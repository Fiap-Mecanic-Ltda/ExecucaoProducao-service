using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;
using ExecucaoService.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExecucaoService.IntegrationTests
{
    [Collection(ColecaoDynamoDb.Nome)]
    public class EstoqueRepositoryTests
    {
        private static readonly DateTime Agora = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        private readonly DynamoDbLocalFixture _db;

        public EstoqueRepositoryTests(DynamoDbLocalFixture db) => _db = db;

        private EstoqueRepository Repo() => new(_db.Dynamo, _db.Opcoes, NullLogger<EstoqueRepository>.Instance);

        private async Task<int> CriarAsync(EstoqueRepository repo, int quantidade, TipoEstoque tipo = TipoEstoque.Peca)
        {
            var id = await repo.ProximoIdAsync();
            await repo.AdicionarAsync(Estoque.Criar(id, $"Item {id}", tipo, quantidade, 1, Agora));
            return id;
        }

        [FatoDynamoDb]
        public async Task Contador_gera_ids_crescentes()
        {
            var repo = Repo();

            var a = await repo.ProximoIdAsync();
            var b = await repo.ProximoIdAsync();

            Assert.Equal(a + 1, b);
        }

        [FatoDynamoDb]
        public async Task Adiciona_obtem_e_lista_por_tipo()
        {
            var repo = Repo();
            var peca = await CriarAsync(repo, 5);
            var insumo = await CriarAsync(repo, 2, TipoEstoque.Insumo);

            var obtido = await repo.ObterAsync(peca);
            Assert.NotNull(obtido);
            Assert.Equal(5, obtido!.QuantidadeAtual);
            Assert.Equal(Agora, obtido.DataUltimaAtualizacao);

            var insumos = await repo.ListarAsync(TipoEstoque.Insumo);
            Assert.Contains(insumos, e => e.Id == insumo);
            Assert.DoesNotContain(insumos, e => e.Id == peca);
        }

        [FatoDynamoDb]
        public async Task Adicionar_id_repetido_lanca_conflito()
        {
            var repo = Repo();
            var id = await CriarAsync(repo, 1);

            await Assert.ThrowsAsync<ConflitoDeConcorrenciaException>(() =>
                repo.AdicionarAsync(Estoque.Criar(id, "Duplicado", TipoEstoque.Peca, 1, 0, Agora)));
        }

        [FatoDynamoDb]
        public async Task Reserva_e_tudo_ou_nada()
        {
            var repo = Repo();
            var a = await CriarAsync(repo, 5);
            var b = await CriarAsync(repo, 1);

            await Assert.ThrowsAsync<EstoqueInsuficienteException>(() =>
                repo.ReservarAsync([new ReservaEstoque(a, 2), new ReservaEstoque(b, 2)], Agora));

            Assert.Equal(0, (await repo.ObterAsync(a))!.QuantidadeReservada);
            Assert.Equal(0, (await repo.ObterAsync(b))!.QuantidadeReservada);
        }

        [FatoDynamoDb]
        public async Task Reserva_baixa_e_liberacao_mantem_o_saldo_coerente()
        {
            var repo = Repo();
            var id = await CriarAsync(repo, 5);

            await repo.ReservarAsync([new ReservaEstoque(id, 3)], Agora);
            var reservado = (await repo.ObterAsync(id))!;
            Assert.Equal(3, reservado.QuantidadeReservada);
            Assert.Equal(2, reservado.QuantidadeDisponivel);

            await Assert.ThrowsAsync<EstoqueInsuficienteException>(() => repo.ReservarAsync([new ReservaEstoque(id, 3)], Agora));

            await repo.BaixarAsync([new ReservaEstoque(id, 2)], Agora);
            var baixado = (await repo.ObterAsync(id))!;
            Assert.Equal(3, baixado.QuantidadeAtual);
            Assert.Equal(1, baixado.QuantidadeReservada);

            await repo.LiberarAsync([new ReservaEstoque(id, 1)], Agora);
            await repo.LiberarAsync([new ReservaEstoque(id, 1)], Agora);   // repetida: não fica negativa
            var liberado = (await repo.ObterAsync(id))!;
            Assert.Equal(0, liberado.QuantidadeReservada);
            Assert.Equal(3, liberado.QuantidadeDisponivel);
        }

        [FatoDynamoDb]
        public async Task Repor_e_remover()
        {
            var repo = Repo();
            var id = await CriarAsync(repo, 1);

            var reposto = await repo.ReporAsync(id, 4, Agora.AddHours(1));
            Assert.Equal(5, reposto.QuantidadeAtual);
            Assert.Equal(Agora.AddHours(1), reposto.DataUltimaAtualizacao);

            await repo.RemoverAsync(id);
            Assert.Null(await repo.ObterAsync(id));
        }

        [FatoDynamoDb]
        public async Task Remover_com_reserva_e_recusado()
        {
            var repo = Repo();
            var id = await CriarAsync(repo, 2);
            await repo.ReservarAsync([new ReservaEstoque(id, 1)], Agora);

            await Assert.ThrowsAsync<RegraDeNegocioException>(() => repo.RemoverAsync(id));
            Assert.NotNull(await repo.ObterAsync(id));
        }
    }
}
