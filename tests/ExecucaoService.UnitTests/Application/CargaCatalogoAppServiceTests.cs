using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.DTOs;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Testes.Compartilhado;

namespace ExecucaoService.UnitTests.Application
{
    public class CargaCatalogoAppServiceTests
    {
        private static readonly DateTime Ontem = new(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc);

        private readonly EstoqueEmMemoria _estoque = new();
        private readonly ServicosEmMemoria _servicos = new();
        private readonly CargaCatalogoAppService _app;

        public CargaCatalogoAppServiceTests() => _app = new CargaCatalogoAppService(_estoque, _servicos);

        private static EstoqueOrigemDto Item(int id, string nome = "Pastilha", int atual = 10) =>
            new() { Id = id, Nome = nome, Tipo = TipoEstoque.Peca, QuantidadeAtual = atual, QuantidadeMinima = 2, DataUltimaAtualizacao = Ontem };

        private static ServicoOficinaOrigemDto Servico(int id, bool ativo = true, DateTime? atualizacao = null) =>
            new() { Id = id, Nome = $"Servico {id}", Descricao = "Desc", ValorBase = 80m, Ativo = ativo, DataCadastro = Ontem, DataAtualizacao = atualizacao };

        [Fact]
        public async Task Carrega_com_os_mesmos_ids_e_sem_reservas()
        {
            var resultado = await _app.CarregarAsync([Item(3), Item(7, "Oleo", 40)], [Servico(5)]);

            Assert.Equal((2, 0, 1, 0), (resultado.EstoqueCriados, resultado.EstoqueJaExistentes, resultado.ServicosCriados, resultado.ServicosJaExistentes));
            Assert.Empty(resultado.Rejeitados);
            var oleo = await _estoque.ObterAsync(7);
            Assert.Equal(("Oleo", 40, 0, Ontem), (oleo!.Nome, oleo.QuantidadeAtual, oleo.QuantidadeReservada, oleo.DataUltimaAtualizacao));
            Assert.Equal(80m, (await _servicos.ObterAsync(5))!.ValorBase);
        }

        [Fact]
        public async Task Proximos_cadastros_continuam_depois_do_maior_id()
        {
            await _app.CarregarAsync([Item(3), Item(42)], [Servico(9)]);

            Assert.Equal(43, await _estoque.ProximoIdAsync());
            Assert.Equal(10, await _servicos.ProximoIdAsync());
        }

        [Fact]
        public async Task Rodar_de_novo_nao_sobrescreve_o_que_ja_foi_carregado()
        {
            await _app.CarregarAsync([Item(1, atual: 10)], [Servico(1)]);
            await _estoque.ReservarAsync([new(1, 4)], Ontem);

            var resultado = await _app.CarregarAsync([Item(1, atual: 99)], [Servico(1)]);

            Assert.Equal((0, 1, 0, 1), (resultado.EstoqueCriados, resultado.EstoqueJaExistentes, resultado.ServicosCriados, resultado.ServicosJaExistentes));
            var item = await _estoque.ObterAsync(1);
            Assert.Equal((10, 4), (item!.QuantidadeAtual, item.QuantidadeReservada));
        }

        [Fact]
        public async Task Servico_inativo_ou_alterado_mantem_estado_e_data()
        {
            var alteracao = Ontem.AddHours(5);

            await _app.CarregarAsync([], [Servico(1, ativo: false), Servico(2, atualizacao: alteracao)]);

            var inativo = await _servicos.ObterAsync(1);
            var alterado = await _servicos.ObterAsync(2);
            Assert.False(inativo!.Ativo);
            Assert.Equal(Ontem, inativo.DataAtualizacao);
            Assert.True(alterado!.Ativo);
            Assert.Equal(alteracao, alterado.DataAtualizacao);
        }

        [Fact]
        public async Task Item_invalido_e_rejeitado_sem_parar_a_carga()
        {
            var resultado = await _app.CarregarAsync([Item(1, nome: " "), Item(2, atual: -1), Item(3)], [Servico(4)]);

            Assert.Equal(1, resultado.EstoqueCriados);
            Assert.Equal(2, resultado.Rejeitados.Count);
            Assert.Contains(resultado.Rejeitados, r => r.StartsWith("Estoque 2"));
        }

        [Fact]
        public async Task Lista_vazia_nao_mexe_nos_contadores()
        {
            var resultado = await _app.CarregarAsync([], []);

            Assert.Equal((0, 0, 0, 0), (resultado.EstoqueCriados, resultado.EstoqueJaExistentes, resultado.ServicosCriados, resultado.ServicosJaExistentes));
            Assert.Equal(1, await _estoque.ProximoIdAsync());
        }
    }
}
