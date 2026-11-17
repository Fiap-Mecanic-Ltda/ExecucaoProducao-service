using Amazon.DynamoDBv2;
using Amazon.Runtime;
using ExecucaoService.Bootstrap.Extensions;
using ExecucaoService.Infrastructure.Persistencia;
using Microsoft.Extensions.Options;

namespace ExecucaoService.IntegrationTests
{
    /// <summary>
    /// Fato que só roda com um DynamoDB Local apontado por DYNAMODB_SERVICE_URL (a CI sobe um
    /// container; localmente, <c>docker compose up dynamodb</c>). Sem a variável, o teste é pulado.
    /// </summary>
    public sealed class FatoDynamoDbAttribute : FactAttribute
    {
        public FatoDynamoDbAttribute()
        {
            if (string.IsNullOrWhiteSpace(DynamoDbLocalFixture.ServiceUrl))
                Skip = "Defina DYNAMODB_SERVICE_URL (ex.: http://localhost:8000) para rodar contra o DynamoDB Local.";
        }
    }

    /// <summary>Cria as tabelas com um prefixo único por execução e apaga tudo no fim.</summary>
    public sealed class DynamoDbLocalFixture : IAsyncLifetime
    {
        public static string? ServiceUrl => Environment.GetEnvironmentVariable("DYNAMODB_SERVICE_URL");

        public IAmazonDynamoDB Dynamo { get; private set; } = null!;
        public IOptions<DynamoDbOptions> Opcoes { get; } = Options.Create(new DynamoDbOptions
        {
            ServiceUrl     = ServiceUrl,
            PrefixoTabelas = $"teste-{Guid.NewGuid():N}"[..14]
        });

        public async Task InitializeAsync()
        {
            if (string.IsNullOrWhiteSpace(ServiceUrl))
                return;

            // O DynamoDB Local aceita qualquer credencial, mas o SDK exige alguma.
            Dynamo = new AmazonDynamoDBClient(new BasicAWSCredentials("local", "local"), DatabaseExtension.CriarConfig(Opcoes.Value));
            await DefinicaoTabelas.CriarQueFaltamAsync(Dynamo, Opcoes.Value);
        }

        public async Task DisposeAsync()
        {
            if (Dynamo is null)
                return;

            foreach (var tabela in DefinicaoTabelas.Todas(Opcoes.Value))
                await Dynamo.DeleteTableAsync(tabela.TableName);

            Dynamo.Dispose();
        }
    }

    [CollectionDefinition(Nome)]
    public sealed class ColecaoDynamoDb : ICollectionFixture<DynamoDbLocalFixture>
    {
        public const string Nome = "DynamoDB Local";
    }
}
