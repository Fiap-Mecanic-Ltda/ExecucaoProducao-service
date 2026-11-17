using ExecucaoService.Domain.Entities;
using ExecucaoService.Infrastructure.Repositories;

namespace ExecucaoService.IntegrationTests
{
    [Collection(ColecaoDynamoDb.Nome)]
    public class ServicoOficinaRepositoryTests
    {
        private static readonly DateTime Agora = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        private readonly DynamoDbLocalFixture _db;

        public ServicoOficinaRepositoryTests(DynamoDbLocalFixture db) => _db = db;

        [FatoDynamoDb]
        public async Task Salva_lista_ativos_e_remove()
        {
            var repo = new ServicoOficinaRepository(_db.Dynamo, _db.Opcoes);
            var ativo = ServicoOficina.Criar(await repo.ProximoIdAsync(), "Alinhamento", "3D", 80.5m, Agora);
            var inativo = ServicoOficina.Criar(await repo.ProximoIdAsync(), "Polimento", null, 200m, Agora);
            inativo.Atualizar(inativo.Nome, null, inativo.ValorBase, false, Agora);
            await repo.SalvarAsync(ativo);
            await repo.SalvarAsync(inativo);

            var lido = await repo.ObterAsync(ativo.Id);
            Assert.Equal("3D", lido!.Descricao);
            Assert.Equal(80.5m, lido.ValorBase);

            var ativos = await repo.ListarAsync(somenteAtivos: true);
            Assert.Contains(ativos, s => s.Id == ativo.Id);
            Assert.DoesNotContain(ativos, s => s.Id == inativo.Id);
            Assert.Equal(2, (await repo.ListarAsync()).Count(s => s.Id == ativo.Id || s.Id == inativo.Id));

            await repo.RemoverAsync(ativo.Id);
            Assert.Null(await repo.ObterAsync(ativo.Id));
        }
    }
}
