using ExecucaoService.Domain.Exceptions;

namespace ExecucaoService.API.Middlewares
{
    /// <summary>
    /// Converte as exceções conhecidas no envelope de erro da API da Fase 3, o mesmo do OS Service
    /// ({ success: false, errors: [...] }) com o status HTTP adequado. Erro inesperado vira 500
    /// sem detalhes internos na resposta (o detalhe fica no log, com o CorrelationId).
    /// </summary>
    public class TratamentoDeErrosMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TratamentoDeErrosMiddleware> _logger;

        public TratamentoDeErrosMiddleware(RequestDelegate next, ILogger<TratamentoDeErrosMiddleware> logger)
        {
            _next   = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex) when (!context.Response.HasStarted)
            {
                var (status, mensagem) = ex switch
                {
                    RegraDeNegocioException             => (StatusCodes.Status400BadRequest, ex.Message),
                    RecursoNaoEncontradoException       => (StatusCodes.Status404NotFound, ex.Message),
                    ConflitoDeConcorrenciaException     => (StatusCodes.Status409Conflict, ex.Message),
                    _                                   => (StatusCodes.Status500InternalServerError,
                                                            "Erro inesperado. Informe o X-Correlation-Id ao suporte."),
                };

                if (status >= 500)
                    _logger.LogError(ex, "Falha ao processar {Metodo} {Caminho}", context.Request.Method, context.Request.Path);
                else
                    _logger.LogInformation("Requisição recusada ({Status}): {Mensagem}", status, ex.Message);

                context.Response.StatusCode = status;
                await context.Response.WriteAsJsonAsync(new { success = false, errors = new[] { mensagem } });
            }
        }
    }

    public static class TratamentoDeErrosMiddlewareExtension
    {
        public static IApplicationBuilder UseTratamentoDeErros(this IApplicationBuilder app)
            => app.UseMiddleware<TratamentoDeErrosMiddleware>();
    }
}
