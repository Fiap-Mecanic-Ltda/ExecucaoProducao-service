using ExecucaoService.Application.DTOs;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Exceptions;

namespace ExecucaoService.Application.AppServices
{
    public interface IServicoOficinaAppService
    {
        Task<IReadOnlyList<ServicoOficinaDto>> ListarAsync(bool somenteAtivos = false, CancellationToken ct = default);
        Task<ServicoOficinaDto> ObterAsync(int id, CancellationToken ct = default);
        Task<ServicoOficinaDto> AdicionarAsync(ServicoOficinaInputDto dto, CancellationToken ct = default);
        Task<ServicoOficinaDto> AtualizarAsync(int id, ServicoOficinaInputDto dto, CancellationToken ct = default);
        Task RemoverAsync(int id, CancellationToken ct = default);
    }

    public class ServicoOficinaAppService : IServicoOficinaAppService
    {
        private readonly IServicoOficinaRepository _repositorio;
        private readonly IRelogio _relogio;

        public ServicoOficinaAppService(IServicoOficinaRepository repositorio, IRelogio relogio)
        {
            _repositorio = repositorio;
            _relogio     = relogio;
        }

        public async Task<IReadOnlyList<ServicoOficinaDto>> ListarAsync(bool somenteAtivos = false, CancellationToken ct = default)
        {
            var servicos = await _repositorio.ListarAsync(somenteAtivos, ct);
            return servicos.OrderBy(s => s.Nome, StringComparer.CurrentCultureIgnoreCase).Select(ServicoOficinaDto.De).ToList();
        }

        public async Task<ServicoOficinaDto> ObterAsync(int id, CancellationToken ct = default) =>
            ServicoOficinaDto.De(await ObterExistenteAsync(id, ct));

        public async Task<ServicoOficinaDto> AdicionarAsync(ServicoOficinaInputDto dto, CancellationToken ct = default)
        {
            ServicoOficina.Criar(1, dto.Nome, dto.Descricao, dto.ValorBase, _relogio.UtcAgora);

            var servico = ServicoOficina.Criar(await _repositorio.ProximoIdAsync(ct), dto.Nome, dto.Descricao, dto.ValorBase, _relogio.UtcAgora);
            if (!dto.Ativo)
                servico.Atualizar(dto.Nome, dto.Descricao, dto.ValorBase, false, _relogio.UtcAgora);

            await _repositorio.SalvarAsync(servico, ct);
            return ServicoOficinaDto.De(servico);
        }

        public async Task<ServicoOficinaDto> AtualizarAsync(int id, ServicoOficinaInputDto dto, CancellationToken ct = default)
        {
            var servico = await ObterExistenteAsync(id, ct);
            servico.Atualizar(dto.Nome, dto.Descricao, dto.ValorBase, dto.Ativo, _relogio.UtcAgora);

            await _repositorio.SalvarAsync(servico, ct);
            return ServicoOficinaDto.De(servico);
        }

        public async Task RemoverAsync(int id, CancellationToken ct = default)
        {
            await ObterExistenteAsync(id, ct);
            await _repositorio.RemoverAsync(id, ct);
        }

        private async Task<ServicoOficina> ObterExistenteAsync(int id, CancellationToken ct) =>
            await _repositorio.ObterAsync(id, ct)
            ?? throw new RecursoNaoEncontradoException($"Serviço da oficina {id} não encontrado.");
    }
}
