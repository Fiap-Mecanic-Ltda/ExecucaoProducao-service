using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;

namespace ExecucaoService.Domain.Entities
{
    /// <summary>
    /// Item de estoque (peça ou insumo). A quantidade reservada pertence a diagnósticos ainda não
    /// executados: só o saldo disponível (atual menos reservada) pode ser reservado de novo.
    /// Reserva, baixa e reposição são gravadas de forma atômica pelo repositório; esta classe
    /// guarda as regras de cadastro.
    /// </summary>
    public class Estoque
    {
        public const int TamanhoMaximoNome = 200;

        public int Id { get; private set; }
        public string Nome { get; private set; } = string.Empty;
        public TipoEstoque Tipo { get; private set; }
        public int QuantidadeAtual { get; private set; }
        public int QuantidadeReservada { get; private set; }
        public int QuantidadeMinima { get; private set; }
        public DateTime DataUltimaAtualizacao { get; private set; }

        public int QuantidadeDisponivel => QuantidadeAtual - QuantidadeReservada;

        /// <summary>Saldo igual ou abaixo do mínimo configurado.</summary>
        public bool EstaBaixoEstoque() => QuantidadeDisponivel <= QuantidadeMinima;

        private Estoque() { }

        public static Estoque Criar(int id, string nome, TipoEstoque tipo, int quantidadeAtual, int quantidadeMinima, DateTime agora)
        {
            if (quantidadeAtual < 0)
                throw new RegraDeNegocioException("A quantidade atual não pode ser negativa.");

            var estoque = new Estoque { Id = id, QuantidadeAtual = quantidadeAtual };
            estoque.AtualizarCadastro(nome, tipo, quantidadeMinima, agora);
            return estoque;
        }

        /// <summary>Recria o item a partir do que está gravado (repositório).</summary>
        public static Estoque Restaurar(int id, string nome, TipoEstoque tipo, int quantidadeAtual,
                                        int quantidadeReservada, int quantidadeMinima, DateTime dataUltimaAtualizacao) =>
            new()
            {
                Id                    = id,
                Nome                  = nome,
                Tipo                  = tipo,
                QuantidadeAtual       = quantidadeAtual,
                QuantidadeReservada   = quantidadeReservada,
                QuantidadeMinima      = quantidadeMinima,
                DataUltimaAtualizacao = dataUltimaAtualizacao
            };

        /// <summary>Nome, tipo e mínimo. Quantidades só mudam por reposição, reserva e baixa.</summary>
        public void AtualizarCadastro(string nome, TipoEstoque tipo, int quantidadeMinima, DateTime agora)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new RegraDeNegocioException("O nome do item de estoque é obrigatório.");
            if (nome.Trim().Length > TamanhoMaximoNome)
                throw new RegraDeNegocioException($"O nome do item de estoque tem no máximo {TamanhoMaximoNome} caracteres.");
            if (!Enum.IsDefined(tipo))
                throw new RegraDeNegocioException("Tipo de estoque inválido. Use 1 (Insumo) ou 2 (Peça).");
            if (quantidadeMinima < 0)
                throw new RegraDeNegocioException("A quantidade mínima não pode ser negativa.");

            Nome                  = nome.Trim();
            Tipo                  = tipo;
            QuantidadeMinima      = quantidadeMinima;
            DataUltimaAtualizacao = agora;
        }

        /// <summary>Um item com peças reservadas ainda tem diagnóstico pendente e não pode sair do cadastro.</summary>
        public void ValidarRemocao()
        {
            if (QuantidadeReservada > 0)
                throw new RegraDeNegocioException(
                    $"O item '{Nome}' tem {QuantidadeReservada} unidade(s) reservada(s) para ordens de serviço e não pode ser removido.");
        }
    }
}
