using Amazon;
using Amazon.DynamoDBv2;
using ExecucaoService.Bootstrap.Extensions;
using ExecucaoService.Infrastructure.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExecucaoService.UnitTests.Bootstrap
{
    public class DatabaseExtensionTests
    {
        [Fact]
        public void Com_ServiceUrl_aponta_para_o_DynamoDB_Local()
        {
            var config = DatabaseExtension.CriarConfig(new DynamoDbOptions { ServiceUrl = "http://localhost:8000", Regiao = "sa-east-1" });

            Assert.StartsWith("http://localhost:8000", config.ServiceURL);
            Assert.Equal("sa-east-1", config.AuthenticationRegion);
        }

        [Fact]
        public void Sem_ServiceUrl_usa_a_regiao_da_AWS()
        {
            var config = DatabaseExtension.CriarConfig(new DynamoDbOptions { Regiao = "us-east-1" });

            Assert.Equal(RegionEndpoint.USEast1, config.RegionEndpoint);
        }

        [Fact]
        public void Nome_da_tabela_usa_o_prefixo_configurado()
        {
            var opcoes = new DynamoDbOptions { PrefixoTabelas = "execucao-teste" };

            Assert.Equal("execucao-teste-estoque", opcoes.Tabela(Tabelas.Estoque));
        }

        [Fact]
        public void Registra_o_cliente_do_DynamoDB_a_partir_da_configuracao()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DynamoDb:ServiceUrl"] = "http://localhost:8000",
                    ["DynamoDb:Regiao"]     = "us-east-1"
                })
                .Build();

            using var provider = new ServiceCollection().AddDatabase(configuration).BuildServiceProvider();

            var cliente = provider.GetRequiredService<IAmazonDynamoDB>();

            Assert.StartsWith("http://localhost:8000", cliente.Config.ServiceURL);
        }
    }
}
