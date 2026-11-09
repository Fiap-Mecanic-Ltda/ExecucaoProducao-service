namespace ExecucaoService.Application.Mensageria
{
    /// <summary>
    /// X-Correlation-Id do fluxo em andamento (requisição HTTP ou mensagem recebida), para que os
    /// eventos publicados levem o mesmo valor no cabeçalho <c>x-correlation-id</c>.
    /// </summary>
    public static class ContextoCorrelacao
    {
        public const string CabecalhoCorrelationId = "correlation-id";
        public const string CabecalhoRequisicao    = "x-correlation-id";

        private static readonly AsyncLocal<string?> _atual = new();

        public static string? CorrelationIdRequisicao
        {
            get => _atual.Value;
            set => _atual.Value = value;
        }
    }
}
