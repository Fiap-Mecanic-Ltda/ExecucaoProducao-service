using ExecucaoService.Application.DTOs;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;
using ExecucaoService.Application.Mensageria;
using MechanicLtda.Contratos;

namespace ExecucaoService.Application.AppServices
{
    public interface IExecucaoAppService
    {
        /// <summary>OS em andamento, na ordem de atendimento; com <paramref name="status"/>, só as desse status.</summary>
        Task<IReadOnlyList<FilaItemDto>> ListarFilaAsync(StatusExecucao? status = null, CancellationToken ct = default);
        Task<ExecucaoDto> ObterAsync(int ordemServicoId, CancellationToken ct = default);

        /// <summary>
        /// Coloca a OS na fila de diagnóstico (comando IniciarDiagnostico). Idempotente: uma OS já
        /// em diagnóstico fica como está, e uma OS com diagnóstico concluído é reaberta, devolvendo
        /// as reservas de estoque.
        /// </summary>
        Task<ExecucaoDto> ReceberAsync(int ordemServicoId, Guid correlationId, int? clienteId, int? veiculoId,
                                       string? descricaoProblema, CancellationToken ct = default);

        Task<ExecucaoDto> IniciarDiagnosticoAsync(int ordemServicoId, CancellationToken ct = default);

        /// <summary>Registra os itens, reserva o estoque e conclui o diagnóstico.</summary>
        Task<ExecucaoDto> RegistrarDiagnosticoAsync(int ordemServicoId, DiagnosticoDto dto, CancellationToken ct = default);

        /// <summary>Pagamento confirmado (comando IniciarExecucao): baixa o estoque reservado e coloca a OS na fila de reparo.</summary>
        Task<ExecucaoDto> LiberarParaExecucaoAsync(int ordemServicoId, CancellationToken ct = default);

        /// <summary>
        /// O mecânico não consegue diagnosticar (peça em falta, veículo sem conserto): devolve as
        /// reservas, cancela a OS na Execução e publica DiagnosticoFalhou.
        /// </summary>
        Task<ExecucaoDto> RegistrarFalhaDiagnosticoAsync(int ordemServicoId, string motivo, CancellationToken ct = default);

        /// <summary>
        /// Modo automático: diagnostica com o primeiro serviço ativo do catálogo e com os itens de
        /// estoque citados na descrição do problema. Sem serviço ou sem saldo, registra a falha.
        /// </summary>
        Task<ExecucaoDto> DiagnosticarAutomaticamenteAsync(int ordemServicoId, decimal valorPeca, CancellationToken ct = default);

        Task<ExecucaoDto> IniciarReparoAsync(int ordemServicoId, CancellationToken ct = default);
        Task<ExecucaoDto> FinalizarAsync(int ordemServicoId, string? observacao, CancellationToken ct = default);

        /// <summary>
        /// Compensação (comando LiberarReservaEstoque): devolve as reservas e cancela a OS.
        /// Idempotente: uma OS já cancelada ou inexistente não muda.
        /// </summary>
        Task<ExecucaoDto?> CancelarAsync(int ordemServicoId, string? motivo, CancellationToken ct = default);
    }

    public class ExecucaoAppService : IExecucaoAppService
    {
        // Ordem de atendimento da fila: o reparo em andamento primeiro, depois o que espera mecânico.
        private static readonly StatusExecucao[] _statusEmAndamento =
        [
            StatusExecucao.EmExecucao,
            StatusExecucao.NaFila,
            StatusExecucao.EmDiagnostico,
            StatusExecucao.AguardandoDiagnostico,
            StatusExecucao.DiagnosticoConcluido,
        ];

        private readonly IExecucaoRepository _execucoes;
        private readonly IEstoqueRepository _estoque;
        private readonly IServicoOficinaRepository _servicos;
        private readonly IRelogio _relogio;
        private readonly IPublicadorEventos _publicador;
        private readonly IMonitoramentoService _monitoramento;

        public ExecucaoAppService(IExecucaoRepository execucoes, IEstoqueRepository estoque,
                                  IServicoOficinaRepository servicos, IRelogio relogio,
                                  IPublicadorEventos publicador, IMonitoramentoService monitoramento)
        {
            _execucoes     = execucoes;
            _estoque       = estoque;
            _servicos      = servicos;
            _relogio       = relogio;
            _publicador    = publicador;
            _monitoramento = monitoramento;
        }

        public async Task<IReadOnlyList<FilaItemDto>> ListarFilaAsync(StatusExecucao? status = null, CancellationToken ct = default)
        {
            if (status is not null && !Enum.IsDefined(status.Value))
                throw new RegraDeNegocioException("Status de execução inválido.");

            var statusConsultados = status is null ? _statusEmAndamento : [status.Value];
            var fila = new List<FilaItemDto>();

            foreach (var s in statusConsultados)
            {
                var execucoes = await _execucoes.ListarPorStatusAsync(s, ct);
                fila.AddRange(execucoes.OrderBy(e => e.DataAlteracaoStatus).Select(FilaItemDto.De));
            }

            return fila;
        }

        public async Task<ExecucaoDto> ObterAsync(int ordemServicoId, CancellationToken ct = default) =>
            ExecucaoDto.De(await ObterExistenteAsync(ordemServicoId, ct));

        public async Task<ExecucaoDto> ReceberAsync(int ordemServicoId, Guid correlationId, int? clienteId, int? veiculoId,
                                                    string? descricaoProblema, CancellationToken ct = default)
        {
            var agora    = _relogio.UtcAgora;
            var execucao = await _execucoes.ObterAsync(ordemServicoId, ct);

            if (execucao is null)
            {
                execucao = ExecucaoOrdemServico.Receber(ordemServicoId, correlationId, clienteId, veiculoId, descricaoProblema, agora);
                await _execucoes.SalvarAsync(execucao, ct);
                _monitoramento.RegistrarPasso(execucao, "Recebida");
                return ExecucaoDto.De(execucao);
            }

            switch (execucao.Status)
            {
                case StatusExecucao.AguardandoDiagnostico or StatusExecucao.EmDiagnostico:
                    return ExecucaoDto.De(execucao);

                case StatusExecucao.DiagnosticoConcluido:
                    var reservas = execucao.Reservas.ToList();
                    execucao.ReabrirDiagnostico(agora);
                    await _execucoes.SalvarAsync(execucao, ct);
                    await _estoque.LiberarAsync(reservas, agora, ct);
                    _monitoramento.RegistrarPasso(execucao, "DiagnosticoReaberto");
                    return ExecucaoDto.De(execucao);

                case StatusExecucao.Cancelada:
                    execucao.RecomecarSaga(correlationId, descricaoProblema, agora);
                    await _execucoes.SalvarAsync(execucao, ct);
                    _monitoramento.RegistrarPasso(execucao, "Recebida");
                    return ExecucaoDto.De(execucao);

                default:
                    throw new RegraDeNegocioException(
                        $"A OS {ordemServicoId} já está em '{execucao.Status}' na Execução e não volta para o diagnóstico.");
            }
        }

        public async Task<ExecucaoDto> IniciarDiagnosticoAsync(int ordemServicoId, CancellationToken ct = default) =>
            ExecucaoDto.De(await AlterarAsync(ordemServicoId, (e, agora) => e.IniciarDiagnostico(agora), "EmDiagnostico", ct));

        public async Task<ExecucaoDto> RegistrarDiagnosticoAsync(int ordemServicoId, DiagnosticoDto dto, CancellationToken ct = default)
        {
            var execucao = await ObterExistenteAsync(ordemServicoId, ct);

            if (execucao.Status is not (StatusExecucao.AguardandoDiagnostico or StatusExecucao.EmDiagnostico))
                throw new RegraDeNegocioException(
                    $"O diagnóstico da OS {ordemServicoId} só pode ser registrado enquanto ela aguarda ou está em diagnóstico. Status atual: '{execucao.Status}'.");
            if (dto.Itens is null || dto.Itens.Count == 0)
                throw new RegraDeNegocioException("O diagnóstico precisa de pelo menos um item (peça, insumo ou serviço).");

            var itens = await MontarItensAsync(dto.Itens, ct);
            return await ConcluirDiagnosticoAsync(execucao, itens, dto.Observacao, ct);
        }

        public async Task<ExecucaoDto> RegistrarFalhaDiagnosticoAsync(int ordemServicoId, string motivo, CancellationToken ct = default)
        {
            var execucao = await ObterExistenteAsync(ordemServicoId, ct);

            if (execucao.Status is not (StatusExecucao.AguardandoDiagnostico or StatusExecucao.EmDiagnostico))
                throw new RegraDeNegocioException(
                    $"A falha de diagnóstico da OS {ordemServicoId} só pode ser registrada enquanto ela aguarda ou está em diagnóstico. Status atual: '{execucao.Status}'.");

            var texto = string.IsNullOrWhiteSpace(motivo) ? "Diagnóstico não concluído pela oficina" : motivo.Trim();
            await CancelarEReservarDevolucaoAsync(execucao, texto, ct);
            await PublicarFalhaAsync(execucao, new DiagnosticoFalhou { Motivo = texto }, texto, ct);

            return ExecucaoDto.De(execucao);
        }

        public async Task<ExecucaoDto> DiagnosticarAutomaticamenteAsync(int ordemServicoId, decimal valorPeca, CancellationToken ct = default)
        {
            var execucao = await ObterExistenteAsync(ordemServicoId, ct);
            var servico  = (await _servicos.ListarAsync(somenteAtivos: true, ct)).OrderBy(s => s.Id).FirstOrDefault();

            if (servico is null)
                return await RegistrarFalhaDiagnosticoAsync(ordemServicoId, "Nenhum serviço ativo no catálogo da oficina", ct);

            var itens = new List<ItemExecucao> { new(servico.Nome, TipoItem.Servico, 1, servico.ValorBase, null, servico.Id) };
            var problema = execucao.DescricaoProblema ?? string.Empty;

            foreach (var estoque in (await _estoque.ListarAsync(null, ct)).OrderBy(e => e.Id))
            {
                if (problema.Contains(estoque.Nome, StringComparison.OrdinalIgnoreCase))
                    itens.Add(new ItemExecucao(estoque.Nome, estoque.Tipo == TipoEstoque.Peca ? TipoItem.Peca : TipoItem.Insumo,
                                               1, valorPeca, estoque.Id, null));
            }

            try
            {
                return await ConcluirDiagnosticoAsync(execucao, itens, "Diagnóstico automático", ct);
            }
            catch (EstoqueInsuficienteException ex)
            {
                return await RegistrarFalhaDiagnosticoAsync(ordemServicoId, ex.Message, ct);
            }
        }

        private async Task<ExecucaoDto> ConcluirDiagnosticoAsync(ExecucaoOrdemServico execucao, IReadOnlyCollection<ItemExecucao> itens,
                                                                 string? observacao, CancellationToken ct)
        {
            var reservas = ExecucaoOrdemServico.ReservasNecessarias(itens);
            var agora    = _relogio.UtcAgora;

            execucao.ConcluirDiagnostico(itens, observacao, agora);

            await _estoque.ReservarAsync(reservas, agora, ct);
            try
            {
                await _execucoes.SalvarAsync(execucao, ct);
            }
            catch
            {
                // A OS não foi gravada: a reserva feita acima não pode ficar presa.
                await _estoque.LiberarAsync(reservas, agora, CancellationToken.None);
                throw;
            }

            _monitoramento.RegistrarPasso(execucao, "DiagnosticoConcluido");
            await _publicador.PublicarAsync(MapeamentoMensagens.Para(execucao, new DiagnosticoConcluido
            {
                Itens      = execucao.Itens.Select(MapeamentoMensagens.ParaContrato).ToList(),
                Observacao = execucao.Observacao,
            }, agora), ct);

            return ExecucaoDto.De(execucao);
        }

        public async Task<ExecucaoDto> LiberarParaExecucaoAsync(int ordemServicoId, CancellationToken ct = default)
        {
            var execucao = await ObterExistenteAsync(ordemServicoId, ct);
            var reservas = execucao.Reservas.ToList();
            var agora    = _relogio.UtcAgora;

            execucao.EntrarNaFila(agora);

            await _estoque.BaixarAsync(reservas, agora, ct);
            await _execucoes.SalvarAsync(execucao, ct);
            _monitoramento.RegistrarPasso(execucao, "NaFila");

            return ExecucaoDto.De(execucao);
        }

        public async Task<ExecucaoDto> IniciarReparoAsync(int ordemServicoId, CancellationToken ct = default)
        {
            var execucao = await AlterarAsync(ordemServicoId, (e, agora) => e.IniciarReparo(agora), "EmExecucao", ct);
            await _publicador.PublicarAsync(MapeamentoMensagens.Para(execucao,
                new ExecucaoIniciada { DataInicio = execucao.DataInicio!.Value }, _relogio.UtcAgora), ct);
            return ExecucaoDto.De(execucao);
        }

        public async Task<ExecucaoDto> FinalizarAsync(int ordemServicoId, string? observacao, CancellationToken ct = default)
        {
            var execucao = await AlterarAsync(ordemServicoId, (e, agora) => e.Finalizar(agora, observacao), "Finalizada", ct);
            await _publicador.PublicarAsync(MapeamentoMensagens.Para(execucao,
                new ExecucaoFinalizada { DataInicio = execucao.DataInicio!.Value, DataFim = execucao.DataFim!.Value }, _relogio.UtcAgora), ct);
            return ExecucaoDto.De(execucao);
        }

        public async Task<ExecucaoDto?> CancelarAsync(int ordemServicoId, string? motivo, CancellationToken ct = default)
        {
            var execucao = await _execucoes.ObterAsync(ordemServicoId, ct);
            if (execucao is null)
                return null;
            if (execucao.Status == StatusExecucao.Cancelada)
                return ExecucaoDto.De(execucao);

            await CancelarEReservarDevolucaoAsync(execucao, motivo ?? "Reservas liberadas a pedido do OS Service", ct);
            return ExecucaoDto.De(execucao);
        }

        /// <summary>Grava a OS cancelada e só então devolve as reservas (vazar uma reserva é melhor que devolvê-la duas vezes).</summary>
        private async Task CancelarEReservarDevolucaoAsync(ExecucaoOrdemServico execucao, string motivo, CancellationToken ct)
        {
            var reservas = execucao.Reservas.ToList();
            var agora    = _relogio.UtcAgora;

            execucao.Cancelar(motivo, agora);

            await _execucoes.SalvarAsync(execucao, ct);
            await _estoque.LiberarAsync(reservas, agora, ct);
            _monitoramento.RegistrarPasso(execucao, "Cancelada");
        }

        private async Task PublicarFalhaAsync<T>(ExecucaoOrdemServico execucao, T evento, string motivo, CancellationToken ct) where T : MensagemSaga
        {
            _monitoramento.RegistrarFalha(execucao.CorrelationId, execucao.OrdemServicoId, typeof(T).Name, motivo);
            await _publicador.PublicarAsync(MapeamentoMensagens.Para(execucao, evento, _relogio.UtcAgora), ct);
        }

        private async Task<ExecucaoOrdemServico> AlterarAsync(int ordemServicoId, Action<ExecucaoOrdemServico, DateTime> alterar,
                                                              string passo, CancellationToken ct)
        {
            var execucao = await ObterExistenteAsync(ordemServicoId, ct);
            alterar(execucao, _relogio.UtcAgora);
            await _execucoes.SalvarAsync(execucao, ct);
            _monitoramento.RegistrarPasso(execucao, passo);
            return execucao;
        }

        /// <summary>Valida cada item contra o estoque e o catálogo e completa descrição e valor de serviços.</summary>
        private async Task<List<ItemExecucao>> MontarItensAsync(IEnumerable<ItemDiagnosticoDto> itens, CancellationToken ct)
        {
            var resultado = new List<ItemExecucao>();

            foreach (var item in itens)
            {
                if (item.Tipo == TipoItem.Servico)
                {
                    var servico = item.ServicoOficinaId is int servicoId
                        ? await _servicos.ObterAsync(servicoId, ct)
                        : null;

                    if (item.ServicoOficinaId is not null && (servico is null || !servico.Ativo))
                        throw new RegraDeNegocioException($"O serviço {item.ServicoOficinaId} não existe ou está inativo no catálogo.");

                    resultado.Add(new ItemExecucao(
                        string.IsNullOrWhiteSpace(item.Descricao) ? servico?.Nome ?? string.Empty : item.Descricao,
                        item.Tipo, item.Quantidade, item.ValorUnitario ?? servico?.ValorBase ?? 0m,
                        item.EstoqueId, item.ServicoOficinaId));
                }
                else
                {
                    var estoque = item.EstoqueId is int estoqueId
                        ? await _estoque.ObterAsync(estoqueId, ct)
                        : null;

                    if (item.EstoqueId is not null && estoque is null)
                        throw new RegraDeNegocioException($"O item de estoque {item.EstoqueId} não existe.");
                    if (item.ValorUnitario is null)
                        throw new RegraDeNegocioException($"Informe o valor unitário de '{item.Descricao ?? estoque?.Nome}'.");

                    resultado.Add(new ItemExecucao(
                        string.IsNullOrWhiteSpace(item.Descricao) ? estoque?.Nome ?? string.Empty : item.Descricao,
                        item.Tipo, item.Quantidade, item.ValorUnitario.Value,
                        item.EstoqueId, item.ServicoOficinaId));
                }
            }

            return resultado;
        }

        private async Task<ExecucaoOrdemServico> ObterExistenteAsync(int ordemServicoId, CancellationToken ct) =>
            await _execucoes.ObterAsync(ordemServicoId, ct)
            ?? throw new RecursoNaoEncontradoException($"A OS {ordemServicoId} não está na Execução.");
    }
}
