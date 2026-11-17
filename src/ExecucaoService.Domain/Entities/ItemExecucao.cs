using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;

namespace ExecucaoService.Domain.Entities
{
    /// <summary>
    /// Item registrado no diagnóstico. É o mesmo formato do <c>ItemOrdemServico</c> do contrato:
    /// peça e insumo apontam para o estoque, serviço aponta para o catálogo.
    /// </summary>
    public sealed record ItemExecucao
    {
        public const int TamanhoMaximoDescricao = 1000;

        public string Descricao { get; }
        public TipoItem Tipo { get; }
        public int Quantidade { get; }
        public decimal ValorUnitario { get; }
        public int? EstoqueId { get; }
        public int? ServicoOficinaId { get; }

        public decimal ValorTotal => Quantidade * ValorUnitario;

        /// <summary>Peças e insumos consomem estoque; serviços não.</summary>
        public bool ConsomeEstoque => Tipo is TipoItem.Peca or TipoItem.Insumo;

        public ItemExecucao(string descricao, TipoItem tipo, int quantidade, decimal valorUnitario, int? estoqueId, int? servicoOficinaId)
        {
            if (string.IsNullOrWhiteSpace(descricao))
                throw new RegraDeNegocioException("A descrição do item é obrigatória.");
            if (descricao.Trim().Length > TamanhoMaximoDescricao)
                throw new RegraDeNegocioException($"A descrição do item tem no máximo {TamanhoMaximoDescricao} caracteres.");
            if (!Enum.IsDefined(tipo))
                throw new RegraDeNegocioException("Tipo de item inválido. Use Peca, Insumo ou Servico.");
            if (quantidade <= 0)
                throw new RegraDeNegocioException("A quantidade do item deve ser maior que zero.");
            if (valorUnitario < 0)
                throw new RegraDeNegocioException("O valor unitário do item não pode ser negativo.");

            if (tipo == TipoItem.Servico)
            {
                if (servicoOficinaId is null or <= 0)
                    throw new RegraDeNegocioException($"O serviço '{descricao.Trim()}' precisa do servicoOficinaId do catálogo.");
                if (estoqueId is not null)
                    throw new RegraDeNegocioException($"O serviço '{descricao.Trim()}' não consome estoque; não informe estoqueId.");
            }
            else
            {
                if (estoqueId is null or <= 0)
                    throw new RegraDeNegocioException($"A peça ou insumo '{descricao.Trim()}' precisa do estoqueId.");
                if (servicoOficinaId is not null)
                    throw new RegraDeNegocioException($"A peça ou insumo '{descricao.Trim()}' não usa servicoOficinaId.");
            }

            Descricao        = descricao.Trim();
            Tipo             = tipo;
            Quantidade       = quantidade;
            ValorUnitario    = decimal.Round(valorUnitario, 2);
            EstoqueId        = estoqueId;
            ServicoOficinaId = servicoOficinaId;
        }
    }
}
