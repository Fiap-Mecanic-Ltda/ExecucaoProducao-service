namespace ExecucaoService.Domain.Exceptions
{
    /// <summary>
    /// A quantidade pedida passa do saldo disponível de um item. Na Saga, vira
    /// DiagnosticoFalhou ou ExecucaoFalhou; na API, HTTP 400.
    /// </summary>
    public class EstoqueInsuficienteException : RegraDeNegocioException
    {
        public int EstoqueId { get; }

        public EstoqueInsuficienteException(int estoqueId, string mensagem) : base(mensagem) => EstoqueId = estoqueId;
    }
}
