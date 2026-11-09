using ExecucaoService.Domain.Entities;

namespace ExecucaoService.Application.Interfaces
{
    public interface IServicoOficinaRepository
    {
        Task<int> ProximoIdAsync(CancellationToken ct = default);
        /// <summary>
        /// Garante que o contador de ids nunca devolva um id menor ou igual a <paramref name="ultimoId"/>
        /// (carga com os ids do MechanicLtda). Nunca diminui o contador.
        /// </summary>
        Task GarantirIdMinimoAsync(int ultimoId, CancellationToken ct = default);
        Task<ServicoOficina?> ObterAsync(int id, CancellationToken ct = default);
        Task<IReadOnlyList<ServicoOficina>> ListarAsync(bool somenteAtivos = false, CancellationToken ct = default);
        Task SalvarAsync(ServicoOficina servico, CancellationToken ct = default);
        Task RemoverAsync(int id, CancellationToken ct = default);
    }
}
