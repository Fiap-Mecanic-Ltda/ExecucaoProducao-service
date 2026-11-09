using ExecucaoService.Infrastructure.Repositories;

namespace ExecucaoService.IntegrationTests
{
    [Collection(ColecaoDynamoDb.Nome)]
    public class ContadorTests
    {
        private readonly DynamoDbLocalFixture _db;

        public ContadorTests(DynamoDbLocalFixture db) => _db = db;

        [FatoDynamoDb]
        public async Task GarantirIdMinimo_sobe_o_contador_e_nunca_desce()
        {
            var repo = new ServicoOficinaRepository(_db.Dynamo, _db.Opcoes);
            var atual = await repo.ProximoIdAsync();

            await repo.GarantirIdMinimoAsync(atual + 500);
            Assert.Equal(atual + 501, await repo.ProximoIdAsync());

            await repo.GarantirIdMinimoAsync(1);
            Assert.Equal(atual + 502, await repo.ProximoIdAsync());
        }
    }
}
