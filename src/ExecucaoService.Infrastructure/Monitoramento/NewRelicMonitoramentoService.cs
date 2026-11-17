using ExecucaoService.Application.Interfaces;
using ExecucaoService.Domain.Entities;
using Microsoft.Extensions.Logging;
using NewRelicAgent = NewRelic.Api.Agent.NewRelic;

namespace ExecucaoService.Infrastructure.Monitoramento
{
    /// <summary>
    /// Eventos de negócio no New Relic: ExecucaoPassoConcluido (cada mudança de status) e
    /// ExecucaoFalhou (falha devolvida à Saga), com o atributo "servico" = execucao-service.
    /// Sem o agente carregado (testes, execução local) a chamada não faz nada, e o evento também
    /// sai no log.
    /// </summary>
    public class NewRelicMonitoramentoService : IMonitoramentoService
    {
        private const string Servico = "execucao-service";
        private const int TamanhoMaximoMensagem = 255;

        private readonly ILogger<NewRelicMonitoramentoService> _logger;

        public NewRelicMonitoramentoService(ILogger<NewRelicMonitoramentoService> logger) => _logger = logger;

        public void RegistrarPasso(ExecucaoOrdemServico execucao, string passo) =>
            Registrar("ExecucaoPassoConcluido", new Dictionary<string, object>
            {
                ["correlationId"]  = execucao.CorrelationId.ToString(),
                ["ordemServicoId"] = execucao.OrdemServicoId,
                ["passo"]          = passo,
                ["status"]         = execucao.Status.ToString(),
                ["valorItens"]     = execucao.ValorTotalItens,
            });

        public void RegistrarFalha(Guid correlationId, int ordemServicoId, string evento, string motivo) =>
            Registrar("ExecucaoFalhou", new Dictionary<string, object>
            {
                ["correlationId"]  = correlationId.ToString(),
                ["ordemServicoId"] = ordemServicoId,
                ["evento"]         = evento,
                ["motivo"]         = motivo.Length <= TamanhoMaximoMensagem ? motivo : motivo[..TamanhoMaximoMensagem],
            });

        private void Registrar(string tipoEvento, Dictionary<string, object> atributos)
        {
            atributos["servico"] = Servico;

            try
            {
                NewRelicAgent.RecordCustomEvent(tipoEvento, atributos);
            }
            catch (Exception ex)
            {
                // Monitoramento nunca derruba a operação de negócio.
                _logger.LogDebug(ex, "Não foi possível enviar o evento {TipoEvento} ao New Relic.", tipoEvento);
            }

            _logger.LogInformation("Evento {TipoEvento}: {@Atributos}", tipoEvento, atributos);
        }
    }
}
