using ExecucaoService.Application.Interfaces;
using ExecucaoService.Domain.Exceptions;
using ExecucaoService.Infrastructure.Mensageria;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExecucaoService.Bootstrap.Extensions
{
    public static class MensageriaExtension
    {
        public const string Secao = "Mensageria";

        /// <summary>
        /// MassTransit 8 com os consumidores dos comandos da Saga. Só é registrado com
        /// Mensageria:Habilitada=true; desligada, a API funciona sozinha (rotas manuais) e não
        /// depende do RabbitMQ.
        ///
        /// Mensageria:Transporte escolhe o broker: "RabbitMq" (padrão) ou "InMemory" (testes).
        /// Não há outbox transacional (ADR-003): o outbox em memória segura os eventos até o
        /// consumidor terminar, e a idempotência fica na tabela execucao-mensagens.
        /// </summary>
        public static IServiceCollection AddMensageria(this IServiceCollection services, IConfiguration configuration)
        {
            var secao = configuration.GetSection(Secao);
            if (!secao.GetValue<bool>("Habilitada"))
            {
                services.AddScoped<IPublicadorEventos, PublicadorDesligado>();
                return services;
            }

            var emMemoria = string.Equals(secao["Transporte"], "InMemory", StringComparison.OrdinalIgnoreCase);

            services.AddScoped<IPublicadorEventos, PublicadorMassTransit>();

            services.AddMassTransit(x =>
            {
                x.AddConsumer<IniciarDiagnosticoConsumer>().Endpoint(e => e.Name = FilasExecucao.IniciarDiagnostico);
                x.AddConsumer<IniciarExecucaoConsumer>().Endpoint(e => e.Name = FilasExecucao.IniciarExecucao);
                x.AddConsumer<LiberarReservaEstoqueConsumer>().Endpoint(e => e.Name = FilasExecucao.LiberarReservaEstoque);

                x.AddConfigureEndpointsCallback((contexto, nome, endpoint) =>
                {
                    // Cada fila recebe um tipo só, então aceita JSON sem cabeçalho de tipo
                    // (publicado por quem não usa MassTransit).
                    if (FilasExecucao.Todas.Contains(nome))
                        endpoint.UseRawJsonDeserializer(RawSerializerOptions.AnyMessageType | RawSerializerOptions.CopyHeaders, isDefault: true);

                    // Erro técnico (DynamoDB fora, conflito de gravação): tenta de novo com
                    // intervalo crescente; esgotado, vai para a fila _error. Erro de negócio já
                    // virou evento de falha no caso de uso e não chega aqui.
                    endpoint.UseMessageRetry(r =>
                    {
                        r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(3));
                        r.Ignore<RecursoNaoEncontradoException>();
                    });

                    // Eventos publicados no consumidor só saem se ele terminar sem erro.
                    endpoint.UseInMemoryOutbox(contexto);
                });

                if (emMemoria)
                {
                    x.UsingInMemory((contexto, cfg) =>
                    {
                        ConfigurarSerializacao(cfg);
                        cfg.ConfigureEndpoints(contexto);
                    });
                }
                else
                {
                    x.UsingRabbitMq((contexto, cfg) =>
                    {
                        var rabbit = configuration.GetSection("RabbitMq");
                        cfg.Host(rabbit["Host"] ?? "localhost", rabbit["VirtualHost"] ?? "/", h =>
                        {
                            h.Username(rabbit["Usuario"] ?? "guest");
                            h.Password(rabbit["Senha"] ?? "guest");
                        });

                        ConfigurarSerializacao(cfg);
                        cfg.ConfigureEndpoints(contexto);
                    });
                }
            });

            return services;
        }

        /// <summary>
        /// JSON puro, sem o envelope do MassTransit, como define o contrato (igual ao OS Service).
        /// O exchange de cada mensagem continua sendo "MechanicLtda.Contratos:NomeDaMensagem".
        /// </summary>
        private static void ConfigurarSerializacao(IBusFactoryConfigurator cfg)
        {
            cfg.UseRawJsonSerializer(RawSerializerOptions.AddTransportHeaders | RawSerializerOptions.CopyHeaders, isDefault: true);
            cfg.UseRawJsonDeserializer(RawSerializerOptions.Default | RawSerializerOptions.CopyHeaders, isDefault: true);
        }
    }
}
