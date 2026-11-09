using Amazon.DynamoDBv2;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExecucaoService.Infrastructure.Persistencia
{
    /// <summary>Cria as tabelas no DynamoDB Local ao subir a API, quando <c>DynamoDb:CriarTabelas</c> está ligado.</summary>
    public class InicializadorDynamoDb : IHostedService
    {
        private readonly IAmazonDynamoDB _dynamo;
        private readonly DynamoDbOptions _opcoes;
        private readonly ILogger<InicializadorDynamoDb> _logger;

        public InicializadorDynamoDb(IAmazonDynamoDB dynamo, IOptions<DynamoDbOptions> opcoes, ILogger<InicializadorDynamoDb> logger)
        {
            _dynamo = dynamo;
            _opcoes = opcoes.Value;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_opcoes.CriarTabelas)
                return;

            await DefinicaoTabelas.CriarQueFaltamAsync(_dynamo, _opcoes, cancellationToken);
            _logger.LogInformation("Tabelas do DynamoDB conferidas (prefixo {Prefixo}).", _opcoes.PrefixoTabelas);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
