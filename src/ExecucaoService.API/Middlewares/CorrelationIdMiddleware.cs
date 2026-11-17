using ExecucaoService.Application.Mensageria;

namespace ExecucaoService.API.Middlewares
{
    /// <summary>
    /// Correlaciona a requisição com o registro do API Gateway. O gateway repassa o seu
    /// requestId no header X-Correlation-Id; aqui esse id entra no escopo de log (aparece em
    /// todas as linhas da requisição) e volta na resposta. Sem o header, um id novo é gerado.
    /// O mesmo id segue no cabeçalho x-correlation-id dos eventos publicados.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-Id";

        private const int TamanhoMaximo = 128;

        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;

        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
        {
            _next   = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

            // Um valor vazio ou grande demais (header forjado) é trocado por um id novo, para
            // não poluir logs nem cabeçalhos de mensagens.
            if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > TamanhoMaximo)
                correlationId = Guid.NewGuid().ToString("n");

            context.Items[HeaderName] = correlationId;
            ContextoCorrelacao.CorrelationIdRequisicao = correlationId;
            context.TraceIdentifier   = correlationId;

            context.Response.OnStarting(() =>
            {
                context.Response.Headers[HeaderName] = correlationId;
                return Task.CompletedTask;
            });

            using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            {
                await _next(context);
            }
        }
    }

    public static class CorrelationIdMiddlewareExtension
    {
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
            => app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
