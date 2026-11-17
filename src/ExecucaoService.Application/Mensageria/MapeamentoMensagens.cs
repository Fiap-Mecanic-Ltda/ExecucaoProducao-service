using ExecucaoService.Domain.Entities;
using MechanicLtda.Contratos;

namespace ExecucaoService.Application.Mensageria
{
    public static class MapeamentoMensagens
    {
        public static ItemOrdemServico ParaContrato(ItemExecucao item) => new()
        {
            Descricao        = item.Descricao,
            Tipo             = item.Tipo.ToString(),
            Quantidade       = item.Quantidade,
            ValorUnitario    = item.ValorUnitario,
            EstoqueId        = item.EstoqueId,
            ServicoOficinaId = item.ServicoOficinaId,
        };

        /// <summary>Preenche os campos comuns a partir da OS na Execução.</summary>
        public static T Para<T>(ExecucaoOrdemServico execucao, T evento, DateTime agora) where T : MensagemSaga =>
            evento with { CorrelationId = execucao.CorrelationId, OrdemServicoId = execucao.OrdemServicoId, DataHora = agora };

        public static T Para<T>(MensagemSaga origem, T evento, DateTime agora) where T : MensagemSaga =>
            evento with { CorrelationId = origem.CorrelationId, OrdemServicoId = origem.OrdemServicoId, DataHora = agora };

        /// <summary>
        /// Chave de idempotência: nome da mensagem, Saga, OS e dataHora. Uma entrega repetida tem o
        /// mesmo corpo e cai na mesma chave; um reenvio legítimo (ex.: IniciarDiagnostico depois de
        /// OrcamentoFalhou) tem outra dataHora e é tratado.
        /// </summary>
        public static string ChaveIdempotencia(MensagemSaga mensagem) =>
            $"{mensagem.GetType().Name}#{mensagem.CorrelationId}#{mensagem.OrdemServicoId}#{mensagem.DataHora.ToUniversalTime():O}";
    }
}
