using ExecucaoService.Application.DTOs;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;

namespace ExecucaoService.Application.AppServices
{
    public interface IEstoqueAppService
    {
        Task<IReadOnlyList<EstoqueDto>> ListarAsync(TipoEstoque? tipo = null, CancellationToken ct = default);
        Task<EstoqueDto> ObterAsync(int id, CancellationToken ct = default);
        Task<EstoqueDto> AdicionarAsync(EstoqueInputDto dto, CancellationToken ct = default);
        Task<EstoqueDto> AtualizarAsync(int id, EstoqueCadastroDto dto, CancellationToken ct = default);
        Task RemoverAsync(int id, CancellationToken ct = default);
        Task<EstoqueDto> ReporAsync(int id, int quantidade, CancellationToken ct = default);
    }

    public class EstoqueAppService : IEstoqueAppService
    {
        private readonly IEstoqueRepository _repositorio;
        private readonly IRelogio _relogio;

        public EstoqueAppService(IEstoqueRepository repositorio, IRelogio relogio)
        {
            _repositorio = repositorio;
            _relogio     = relogio;
        }

        public async Task<IReadOnlyList<EstoqueDto>> ListarAsync(TipoEstoque? tipo = null, CancellationToken ct = default)
        {
            if (tipo is not null && !Enum.IsDefined(tipo.Value))
                throw new RegraDeNegocioException("Tipo de estoque inválido. Use 1 (Insumo) ou 2 (Peça).");

            var itens = await _repositorio.ListarAsync(tipo, ct);
            return itens.OrderBy(e => e.Nome, StringComparer.CurrentCultureIgnoreCase).Select(EstoqueDto.De).ToList();
        }

        public async Task<EstoqueDto> ObterAsync(int id, CancellationToken ct = default) =>
            EstoqueDto.De(await ObterExistenteAsync(id, ct));

        public async Task<EstoqueDto> AdicionarAsync(EstoqueInputDto dto, CancellationToken ct = default)
        {
            // Valida antes de consumir um id do contador.
            Estoque.Criar(1, dto.Nome, dto.Tipo, dto.QuantidadeAtual, dto.QuantidadeMinima, _relogio.UtcAgora);

            var estoque = Estoque.Criar(await _repositorio.ProximoIdAsync(ct), dto.Nome, dto.Tipo,
                dto.QuantidadeAtual, dto.QuantidadeMinima, _relogio.UtcAgora);

            await _repositorio.AdicionarAsync(estoque, ct);
            return EstoqueDto.De(estoque);
        }

        public async Task<EstoqueDto> AtualizarAsync(int id, EstoqueCadastroDto dto, CancellationToken ct = default)
        {
            var estoque = await ObterExistenteAsync(id, ct);
            estoque.AtualizarCadastro(dto.Nome, dto.Tipo, dto.QuantidadeMinima, _relogio.UtcAgora);

            await _repositorio.AtualizarCadastroAsync(estoque, ct);
            return EstoqueDto.De(estoque);
        }

        public async Task RemoverAsync(int id, CancellationToken ct = default)
        {
            var estoque = await ObterExistenteAsync(id, ct);
            estoque.ValidarRemocao();

            await _repositorio.RemoverAsync(id, ct);
        }

        public async Task<EstoqueDto> ReporAsync(int id, int quantidade, CancellationToken ct = default)
        {
            if (quantidade <= 0)
                throw new RegraDeNegocioException("A quantidade de entrada deve ser maior que zero.");

            await ObterExistenteAsync(id, ct);
            return EstoqueDto.De(await _repositorio.ReporAsync(id, quantidade, _relogio.UtcAgora, ct));
        }

        private async Task<Estoque> ObterExistenteAsync(int id, CancellationToken ct) =>
            await _repositorio.ObterAsync(id, ct)
            ?? throw new RecursoNaoEncontradoException($"Item de estoque {id} não encontrado.");
    }
}
