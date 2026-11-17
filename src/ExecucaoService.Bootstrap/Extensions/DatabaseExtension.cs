using Amazon;
using Amazon.DynamoDBv2;
using ExecucaoService.Infrastructure.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ExecucaoService.Bootstrap.Extensions
{
    public static class DatabaseExtension
    {
        /// <summary>
        /// Registra o cliente do DynamoDB. Com <c>DynamoDb:ServiceUrl</c> preenchido, aponta para
        /// o DynamoDB Local; sem ele, usa a região e as credenciais padrão da AWS (role da
        /// instância no cluster).
        /// </summary>
        public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<DynamoDbOptions>(configuration.GetSection(DynamoDbOptions.Secao));

            services.AddSingleton<IAmazonDynamoDB>(provider =>
                new AmazonDynamoDBClient(CriarConfig(provider.GetRequiredService<IOptions<DynamoDbOptions>>().Value)));

            return services;
        }

        public static AmazonDynamoDBConfig CriarConfig(DynamoDbOptions opcoes)
        {
            var config = new AmazonDynamoDBConfig();

            if (!string.IsNullOrWhiteSpace(opcoes.ServiceUrl))
            {
                config.ServiceURL           = opcoes.ServiceUrl;
                config.AuthenticationRegion = opcoes.Regiao;
            }
            else
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(opcoes.Regiao);
            }

            return config;
        }
    }
}
