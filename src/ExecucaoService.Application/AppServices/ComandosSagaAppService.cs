using ExecucaoService.Application.Interfaces;
using ExecucaoService.Application.Mensageria;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;
using MechanicLtda.Contratos;
using Microsoft.Extensions.Logging;

namespace ExecucaoService.Application.AppServices
{
    /// <summary>
    /// Os três comandos que a Saga do OS Service envia para a Execução. Erro de negócio vira
    /// evento de falha (contrato); erro técnico sobe para o consumidor, que tenta de novo.
    /// Cada mensagem é tratada uma vez só (<see cref="IRegistroMensagens"/>); uma entrega repetida
    /// republica a resposta registrada, o que cobre uma falha entre gravar e publicar.
    /// </summary>
    public interface IComandosSagaAppService
    {
        Task TratarAsync(IniciarDiagnostico comando, CancellationToken ct = default);
        Task TratarAsync(IniciarExecucao comando, CancellationToken ct = default);
        Task TratarAsync(LiberarReservaEstoque comando, CancellationToken ct = default);
    }

    public class ComandosSagaAppService : IComandosSagaAppService
    {
        private readonly IExecucaoAppService _execucao;
        private readonly IExecucaoRepository _execucoes;
        private readonly IRegistroMensagens _registro;
        private readonly IPublicadorEventos _publicador;
        private readonly IMonitoramentoService _monitoramento;
        private readonly IRelogio _relogio;
        private readonly ILogger<ComandosSagaAppService> _logger;

        public ComandosSagaAppService(IExecucaoAppService execucao, IExecucaoRepository execucoes, IRegistroMensagens registro,
                                      IPublicadorEventos publicador, IMonitoramentoService monitoramento, IRelogio relogio,
                                      ILogger<ComandosSagaAppService> logger)
        {
            _execucao      = execucao;
            _execucoes     = execucoes;
            _registro      = registro;
            _publicador    = publicador;
            _monitoramento = monitoramento;
            _relogio       = relogio;
            _logger        = logger;
        }

        /// <summary>Entra na fila de diagnóstico; se a OS já passou do diagnóstico, responde DiagnosticoFalhou.</summary>
        public Task TratarAsync(IniciarDiagnostico comando, CancellationToken ct = default) =>
            UmaVezAsync(comando, async () =>
            {
                try
                {
                    await _execucao.ReceberAsync(comando.OrdemServicoId, comando.CorrelationId, comando.ClienteId,
                                                 comando.VeiculoId, comando.DescricaoProblema, ct);
                    return null;
                }
                catch (RegraDeNegocioException ex)
                {
                    return await ResponderFalhaAsync(comando, new DiagnosticoFalhou { Motivo = ex.Message }, ex.Message, ct);
                }
            }, ct);

        /// <summary>
        /// Pagamento confirmado: baixa o estoque reservado e põe a OS na fila de reparo. Sem
        /// diagnóstico concluído ou sem saldo, devolve as reservas, cancela e responde ExecucaoFalhou.
        /// </summary>
        public Task TratarAsync(IniciarExecucao comando, CancellationToken ct = default) =>
            UmaVezAsync(comando, async () =>
            {
                var execucao = await _execucoes.ObterAsync(comando.OrdemServicoId, ct);

                if (execucao is null)
                {
                    var desconhecida = $"A OS {comando.OrdemServicoId} não passou pelo diagnóstico na Execução.";
                    return await ResponderFalhaAsync(comando, new ExecucaoFalhou { Motivo = desconhecida }, desconhecida, ct);
                }

                if (execucao.Status is StatusExecucao.NaFila or StatusExecucao.EmExecucao or StatusExecucao.Finalizada)
                {
                    _logger.LogInformation("IniciarExecucao repetido para a OS {OrdemServicoId} (status {Status}); nada a fazer.",
                                           comando.OrdemServicoId, execucao.Status);
                    return null;
                }

                string motivo;
                if (execucao.Status != StatusExecucao.DiagnosticoConcluido)
                {
                    motivo = $"A OS {comando.OrdemServicoId} está em '{execucao.Status}' na Execução e não pode ser executada.";
                }
                else
                {
                    try
                    {
                        await _execucao.LiberarParaExecucaoAsync(comando.OrdemServicoId, ct);
                        return null;
                    }
                    catch (EstoqueInsuficienteException ex)
                    {
                        motivo = ex.Message;
                    }
                }

                if (!execucao.Status.EhFinal())
                    await _execucao.CancelarAsync(comando.OrdemServicoId, motivo, ct);

                return await ResponderFalhaAsync(comando, new ExecucaoFalhou { Motivo = motivo }, motivo, ct);
            }, ct);

        /// <summary>Compensação sem resposta: devolve as reservas e cancela. OS já executada fica como está.</summary>
        public Task TratarAsync(LiberarReservaEstoque comando, CancellationToken ct = default) =>
            UmaVezAsync(comando, async () =>
            {
                var execucao = await _execucoes.ObterAsync(comando.OrdemServicoId, ct);
                if (execucao is null || execucao.Status.EhFinal())
                    return null;

                if (execucao.Status is StatusExecucao.EmExecucao)
                {
                    _logger.LogWarning("LiberarReservaEstoque ignorado: a OS {OrdemServicoId} já está em reparo.", comando.OrdemServicoId);
                    return null;
                }

                var motivo = string.IsNullOrWhiteSpace(comando.Motivo) ? "Reservas liberadas a pedido do OS Service" : comando.Motivo;
                await _execucao.CancelarAsync(comando.OrdemServicoId, motivo, ct);
                return null;
            }, ct);

        /// <summary>Trata a mensagem uma vez; <paramref name="tratar"/> devolve a resposta publicada, se houve.</summary>
        private async Task UmaVezAsync(MensagemSaga mensagem, Func<Task<MensagemSaga?>> tratar, CancellationToken ct)
        {
            var chave     = MapeamentoMensagens.ChaveIdempotencia(mensagem);
            var anterior  = await _registro.ObterAsync(chave, ct);

            if (anterior is not null)
            {
                _logger.LogInformation("Mensagem {Mensagem} da OS {OrdemServicoId} já tratada; resposta republicada: {Resposta}.",
                                       mensagem.GetType().Name, mensagem.OrdemServicoId, anterior.Resposta?.GetType().Name ?? "nenhuma");
                if (anterior.Resposta is not null)
                    await _publicador.PublicarAsync(anterior.Resposta, ct);
                return;
            }

            var resposta = await tratar();
            await _registro.RegistrarAsync(chave, resposta, _relogio.UtcAgora, ct);
        }

        private async Task<MensagemSaga> ResponderFalhaAsync<T>(MensagemSaga origem, T evento, string motivo, CancellationToken ct) where T : MensagemSaga
        {
            _logger.LogWarning("{Evento} para a OS {OrdemServicoId}: {Motivo}", typeof(T).Name, origem.OrdemServicoId, motivo);
            _monitoramento.RegistrarFalha(origem.CorrelationId, origem.OrdemServicoId, typeof(T).Name, motivo);

            var resposta = MapeamentoMensagens.Para(origem, evento, _relogio.UtcAgora);
            await _publicador.PublicarAsync(resposta, ct);
            return resposta;
        }
    }
}
