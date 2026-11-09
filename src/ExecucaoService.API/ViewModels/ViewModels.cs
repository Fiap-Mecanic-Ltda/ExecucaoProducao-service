using ExecucaoService.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace ExecucaoService.API.ViewModels
{
    public class EstoqueCreateViewModel
    {
        [Required, MaxLength(200)]
        public string Nome { get; set; } = string.Empty;

        [Required]
        public TipoEstoque Tipo { get; set; }

        [Range(0, int.MaxValue)]
        public int QuantidadeAtual { get; set; }

        [Range(0, int.MaxValue)]
        public int QuantidadeMinima { get; set; }
    }

    public class EstoqueUpdateViewModel
    {
        [Required, MaxLength(200)]
        public string Nome { get; set; } = string.Empty;

        [Required]
        public TipoEstoque Tipo { get; set; }

        [Range(0, int.MaxValue)]
        public int QuantidadeMinima { get; set; }
    }

    public class EstoqueReposicaoViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "A quantidade de entrada deve ser maior que zero.")]
        public int Quantidade { get; set; }
    }

    public class ServicoOficinaViewModel
    {
        [Required, MaxLength(200)]
        public string Nome { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Descricao { get; set; }

        [Range(0, double.MaxValue)]
        public decimal ValorBase { get; set; }

        public bool Ativo { get; set; } = true;
    }

    public class EntradaManualViewModel
    {
        [Range(1, int.MaxValue)]
        public int OrdemServicoId { get; set; }

        public int? ClienteId { get; set; }

        public int? VeiculoId { get; set; }

        [MaxLength(1000)]
        public string? DescricaoProblema { get; set; }
    }

    public class ItemDiagnosticoViewModel
    {
        [MaxLength(1000)]
        public string? Descricao { get; set; }

        [Required]
        public TipoItem Tipo { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantidade { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? ValorUnitario { get; set; }

        public int? EstoqueId { get; set; }

        public int? ServicoOficinaId { get; set; }
    }

    public class DiagnosticoViewModel
    {
        [Required, MinLength(1)]
        public List<ItemDiagnosticoViewModel> Itens { get; set; } = [];

        [MaxLength(1000)]
        public string? Observacao { get; set; }
    }

    public class FinalizacaoViewModel
    {
        [MaxLength(1000)]
        public string? Observacao { get; set; }
    }

    public class FalhaDiagnosticoViewModel
    {
        [Required, MaxLength(1000)]
        public string Motivo { get; set; } = string.Empty;
    }

    public class CancelamentoViewModel
    {
        [MaxLength(1000)]
        public string? Motivo { get; set; }
    }
}
