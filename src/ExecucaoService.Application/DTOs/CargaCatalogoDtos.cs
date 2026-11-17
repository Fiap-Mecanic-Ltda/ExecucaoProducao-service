using ExecucaoService.Domain.Enums;

namespace ExecucaoService.Application.DTOs
{
    /// <summary>Item de estoque como o MechanicLtda devolve em GET /api/estoque.</summary>
    public class EstoqueOrigemDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public TipoEstoque Tipo { get; set; }
        public int QuantidadeAtual { get; set; }
        public int QuantidadeMinima { get; set; }
        public DateTime DataUltimaAtualizacao { get; set; }
    }

    /// <summary>Serviço como o MechanicLtda devolve em GET /api/servicos-oficina.</summary>
    public class ServicoOficinaOrigemDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public decimal ValorBase { get; set; }
        public bool Ativo { get; set; } = true;
        public DateTime DataCadastro { get; set; }
        public DateTime? DataAtualizacao { get; set; }
    }

    public record ResultadoCargaDto(
        int EstoqueCriados,
        int EstoqueJaExistentes,
        int ServicosCriados,
        int ServicosJaExistentes,
        IReadOnlyList<string> Rejeitados);
}
