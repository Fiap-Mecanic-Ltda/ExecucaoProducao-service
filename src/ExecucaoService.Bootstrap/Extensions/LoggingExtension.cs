using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ExecucaoService.Bootstrap.Extensions
{
    public static class LoggingExtension
    {
        /// <summary>
        /// Fora de Development, logs em JSON (um objeto por linha) com os escopos — entre eles o
        /// CorrelationId, que liga a linha de log à requisição do API Gateway e, a partir da
        /// etapa da Saga, às mensagens trocadas com os outros serviços. Mesmo formato do
        /// MechanicLtda, para as consultas no New Relic valerem para os dois.
        /// </summary>
        public static WebApplicationBuilder AddLogsEstruturados(this WebApplicationBuilder builder)
        {
            if (builder.Environment.IsDevelopment())
                return builder;

            builder.Logging.ClearProviders();
            builder.Logging.AddJsonConsole(options =>
            {
                options.IncludeScopes   = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
                options.JsonWriterOptions = new JsonWriterOptions
                {
                    Indented = false,
                    Encoder  = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                };
            });

            return builder;
        }
    }
}
