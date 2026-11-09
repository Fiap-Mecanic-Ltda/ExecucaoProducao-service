using ExecucaoService.Domain.Entities;

namespace ExecucaoService.Application.Interfaces
{
    public interface IServicoOficinaRepository
    {
        Task<int> ProximoIdAsync(CancellationToken ct = default);
        Task<ServicoOficina?> ObterAsync(int id, CancellationToken ct = default);
        Task<IReadOnlyList<ServicoOficina>> ListarAsync(bool somenteAtivos = false, CancellationToken ct = default);
        Task SalvarAsync(ServicoOficina servico, CancellationToken ct = default);
        Task RemoverAsync(int id, CancellationToken ct = default);
    }
}
