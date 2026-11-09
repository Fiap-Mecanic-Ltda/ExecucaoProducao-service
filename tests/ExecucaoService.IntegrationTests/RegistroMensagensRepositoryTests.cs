using Amazon.DynamoDBv2.Model;
using ExecucaoService.Infrastructure.Persistencia;
using ExecucaoService.Infrastructure.Repositories;

namespace ExecucaoService.IntegrationTests
{
    [Collection(ColecaoDynamoDb.Nome)]
    public class RegistroMensagensRepositoryTests
    {
        private readonly DynamoDbLocalFixture _db;

        public RegistroMensagensRepositoryTests(DynamoDbLocalFixture db) => _db = db;

        [FatoDynamoDb]
        public async Task Registra_a_mensagem_com_ttl_de_7_dias()
        {
            var repo  = new RegistroMensagensRepository(_db.Dynamo, _db.Opcoes);
            var chave = $"IniciarDiagnostico#{Guid.NewGuid()}#42#2026-11-09T12:00:00.0000000Z";
            var agora = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

            Assert.Null(await repo.ObterAsync(chave));
            await repo.RegistrarAsync(chave, null, agora);
            var tratada = await repo.ObterAsync(chave);
            Assert.NotNull(tratada);
            Assert.Null(tratada!.Resposta);

            var item = await _db.Dynamo.GetItemAsync(new GetItemRequest
            {
                TableName = _db.Opcoes.Value.Tabela(Tabelas.Mensagens),
                Key       = new() { ["pk"] = new AttributeValue { S = chave } }
            });
            Assert.Equal(new DateTimeOffset(agora.AddDays(7)).ToUnixTimeSeconds().ToString(), item.Item[RegistroMensagensRepository.AtributoTtl].N);
        }
    
        [FatoDynamoDb]
        public async Task Guarda_e_devolve_a_resposta_publicada()
        {
            var repo  = new RegistroMensagensRepository(_db.Dynamo, _db.Opcoes);
            var chave = $"IniciarExecucao#{Guid.NewGuid()}#42#2026-11-09T12:00:00.0000000Z";
            var resposta = new MechanicLtda.Contratos.ExecucaoFalhou
            {
                CorrelationId = Guid.NewGuid(), OrdemServicoId = 42, DataHora = new DateTime(2026, 11, 9, 12, 0, 1, DateTimeKind.Utc), Motivo = "Sem saldo",
            };

            await repo.RegistrarAsync(chave, resposta, DateTime.UtcNow);

            Assert.Equal(resposta, (await repo.ObterAsync(chave))!.Resposta);
        }
    }
}
