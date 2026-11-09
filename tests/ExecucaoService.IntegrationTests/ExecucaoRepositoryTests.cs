using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;
using ExecucaoService.Infrastructure.Repositories;

namespace ExecucaoService.IntegrationTests
{
    [Collection(ColecaoDynamoDb.Nome)]
    public class ExecucaoRepositoryTests
    {
        private static readonly DateTime Agora = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        private readonly DynamoDbLocalFixture _db;

        public ExecucaoRepositoryTests(DynamoDbLocalFixture db) => _db = db;

        private ExecucaoRepository Repo() => new(_db.Dynamo, _db.Opcoes);

        private static int NovaOs() => Random.Shared.Next(100_000, int.MaxValue);

        [FatoDynamoDb]
        public async Task Grava_e_le_a_execucao_com_itens_e_historico()
        {
            var repo = Repo();
            var os = NovaOs();
            var saga = Guid.NewGuid();
            var execucao = ExecucaoOrdemServico.Receber(os, saga, 7, 3, "Freio chiando", Agora);
            await repo.SalvarAsync(execucao);

            execucao.ConcluirDiagnostico(
            [
                new ItemExecucao("Pastilha", TipoItem.Peca, 2, 89.90m, 15, null),
                new ItemExecucao("Troca", TipoItem.Servico, 1, 120m, null, 3),
            ], "Discos ok", Agora.AddMinutes(30));
            await repo.SalvarAsync(execucao);

            var lida = await repo.ObterAsync(os);

            Assert.NotNull(lida);
            Assert.Equal(StatusExecucao.DiagnosticoConcluido, lida!.Status);
            Assert.Equal(saga, lida.CorrelationId);
            Assert.Equal(7, lida.ClienteId);
            Assert.Equal(2, lida.Itens.Count);
            Assert.Equal(299.80m, lida.ValorTotalItens);
            Assert.Equal(new ReservaEstoque(15, 2), Assert.Single(lida.Reservas));
            Assert.Equal(Agora.AddMinutes(30), lida.DataConclusaoDiagnostico);
            Assert.Equal(
                [StatusExecucao.AguardandoDiagnostico, StatusExecucao.EmDiagnostico, StatusExecucao.DiagnosticoConcluido],
                lida.Historico.Select(h => h.StatusNovo));
            Assert.Empty(lida.HistoricoNaoGravado);
        }

        [FatoDynamoDb]
        public async Task Gravacao_com_versao_antiga_lanca_conflito()
        {
            var repo = Repo();
            var os = NovaOs();
            await repo.SalvarAsync(ExecucaoOrdemServico.Receber(os, Guid.NewGuid(), null, null, null, Agora));

            var primeira = (await repo.ObterAsync(os))!;
            var segunda  = (await repo.ObterAsync(os))!;

            primeira.IniciarDiagnostico(Agora.AddMinutes(1));
            await repo.SalvarAsync(primeira);

            segunda.Cancelar("concorrente", Agora.AddMinutes(2));
            await Assert.ThrowsAsync<ConflitoDeConcorrenciaException>(() => repo.SalvarAsync(segunda));

            Assert.Equal(StatusExecucao.EmDiagnostico, (await repo.ObterAsync(os))!.Status);
        }

        [FatoDynamoDb]
        public async Task Lista_por_status_pelo_indice()
        {
            var repo = Repo();
            var os = NovaOs();
            var execucao = ExecucaoOrdemServico.Receber(os, Guid.NewGuid(), null, null, null, Agora);
            execucao.IniciarDiagnostico(Agora.AddMinutes(1));
            await repo.SalvarAsync(execucao);

            var emDiagnostico = await repo.ListarPorStatusAsync(StatusExecucao.EmDiagnostico);
            var aguardando    = await repo.ListarPorStatusAsync(StatusExecucao.AguardandoDiagnostico);

            Assert.Contains(emDiagnostico, e => e.OrdemServicoId == os);
            Assert.DoesNotContain(aguardando, e => e.OrdemServicoId == os);
        }

        [FatoDynamoDb]
        public async Task Os_inexistente_devolve_nulo()
        {
            Assert.Null(await Repo().ObterAsync(NovaOs()));
        }
    }
}
