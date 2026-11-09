using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;

namespace ExecucaoService.Application.Interfaces
{
    public interface IExecucaoRepository
    {
        Task<ExecucaoOrdemServico?> ObterAsync(int ordemServicoId, CancellationToken ct = default);

        /// <summary>OS num status, da mais antiga para a mais nova (ordem da fila).</summary>
        Task<IReadOnlyList<ExecucaoOrdemServico>> ListarPorStatusAsync(StatusExecucao status, CancellationToken ct = default);

        /// <summary>
        /// Grava a OS e o histórico novo. Falha com
        /// <see cref="Domain.Exceptions.ConflitoDeConcorrenciaException"/> se a versão gravada mudou.
        /// </summary>
        Task SalvarAsync(ExecucaoOrdemServico execucao, CancellationToken ct = default);
    }
}
