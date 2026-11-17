using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;

namespace ExecucaoService.Application.DTOs
{
    public sealed record EstoqueInputDto(string Nome, TipoEstoque Tipo, int QuantidadeAtual, int QuantidadeMinima);

    public sealed record EstoqueCadastroDto(string Nome, TipoEstoque Tipo, int QuantidadeMinima);

    public sealed record EstoqueDto(
        int Id, string Nome, TipoEstoque Tipo, int QuantidadeAtual, int QuantidadeReservada,
        int QuantidadeDisponivel, int QuantidadeMinima, bool BaixoEstoque, DateTime DataUltimaAtualizacao)
    {
        public static EstoqueDto De(Estoque e) =>
            new(e.Id, e.Nome, e.Tipo, e.QuantidadeAtual, e.QuantidadeReservada, e.QuantidadeDisponivel,
                e.QuantidadeMinima, e.EstaBaixoEstoque(), e.DataUltimaAtualizacao);
    }
}
