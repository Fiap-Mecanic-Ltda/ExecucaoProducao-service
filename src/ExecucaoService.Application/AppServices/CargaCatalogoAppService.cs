using ExecucaoService.Application.DTOs;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Exceptions;

namespace ExecucaoService.Application.AppServices
{
    public interface ICargaCatalogoAppService
    {
        Task<ResultadoCargaDto> CarregarAsync(
            IReadOnlyCollection<EstoqueOrigemDto> estoque,
            IReadOnlyCollection<ServicoOficinaOrigemDto> servicos,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Carga inicial do estoque e do catálogo de serviços vindos do MechanicLtda, mantendo os ids
    /// (as OS antigas e as telas apontam para eles). Pode rodar mais de uma vez: um id que já existe
    /// na Execução é mantido como está, porque depois da virada ele já pode ter reservas e baixas.
    /// No fim, os contadores passam do maior id carregado, para os próximos cadastros não colidirem.
    /// </summary>
    public class CargaCatalogoAppService : ICargaCatalogoAppService
    {
        private readonly IEstoqueRepository _estoque;
        private readonly IServicoOficinaRepository _servicos;

        public CargaCatalogoAppService(IEstoqueRepository estoque, IServicoOficinaRepository servicos)
        {
            _estoque  = estoque;
            _servicos = servicos;
        }

        public async Task<ResultadoCargaDto> CarregarAsync(
            IReadOnlyCollection<EstoqueOrigemDto> estoque,
            IReadOnlyCollection<ServicoOficinaOrigemDto> servicos,
            CancellationToken ct = default)
        {
            var rejeitados = new List<string>();
            int estoqueCriados = 0, estoqueExistentes = 0, servicosCriados = 0, servicosExistentes = 0;

            foreach (var origem in estoque.OrderBy(e => e.Id))
            {
                if (await _estoque.ObterAsync(origem.Id, ct) is not null)
                {
                    estoqueExistentes++;
                    continue;
                }

                try
                {
                    var item = Estoque.Criar(origem.Id, origem.Nome, origem.Tipo, origem.QuantidadeAtual,
                                             origem.QuantidadeMinima, origem.DataUltimaAtualizacao);
                    await _estoque.AdicionarAsync(item, ct);
                    estoqueCriados++;
                }
                catch (RegraDeNegocioException ex)
                {
                    rejeitados.Add($"Estoque {origem.Id} ({origem.Nome}): {ex.Message}");
                }
            }

            foreach (var origem in servicos.OrderBy(s => s.Id))
            {
                if (await _servicos.ObterAsync(origem.Id, ct) is not null)
                {
                    servicosExistentes++;
                    continue;
                }

                try
                {
                    var servico = ServicoOficina.Criar(origem.Id, origem.Nome, origem.Descricao, origem.ValorBase, origem.DataCadastro);
                    if (!origem.Ativo || origem.DataAtualizacao is not null)
                        servico.Atualizar(origem.Nome, origem.Descricao, origem.ValorBase, origem.Ativo,
                                          origem.DataAtualizacao ?? origem.DataCadastro);
                    await _servicos.SalvarAsync(servico, ct);
                    servicosCriados++;
                }
                catch (RegraDeNegocioException ex)
                {
                    rejeitados.Add($"Serviço {origem.Id} ({origem.Nome}): {ex.Message}");
                }
            }

            if (estoque.Count > 0)
                await _estoque.GarantirIdMinimoAsync(estoque.Max(e => e.Id), ct);
            if (servicos.Count > 0)
                await _servicos.GarantirIdMinimoAsync(servicos.Max(s => s.Id), ct);

            return new ResultadoCargaDto(estoqueCriados, estoqueExistentes, servicosCriados, servicosExistentes, rejeitados);
        }
    }
}
