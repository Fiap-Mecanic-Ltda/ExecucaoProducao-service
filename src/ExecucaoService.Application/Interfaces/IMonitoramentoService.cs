using ExecucaoService.Domain.Entities;

namespace ExecucaoService.Application.Interfaces
{
    /// <summary>Eventos de negócio no New Relic (ExecucaoPassoConcluido e ExecucaoFalhou).</summary>
    public interface IMonitoramentoService
    {
        /// <summary>A OS mudou de status na Execução.</summary>
        void RegistrarPasso(ExecucaoOrdemServico execucao, string passo);

        /// <summary>A Execução respondeu com uma falha à Saga (DiagnosticoFalhou ou ExecucaoFalhou).</summary>
        void RegistrarFalha(Guid correlationId, int ordemServicoId, string evento, string motivo);
    }
}
