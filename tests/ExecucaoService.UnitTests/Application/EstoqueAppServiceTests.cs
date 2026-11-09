using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.DTOs;
using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;
using ExecucaoService.Testes.Compartilhado;

namespace ExecucaoService.UnitTests.Application
{
    public class EstoqueAppServiceTests
    {
        private readonly EstoqueEmMemoria _repo = new();
        private readonly RelogioFixo _relogio = new();
        private readonly EstoqueAppService _app;

        public EstoqueAppServiceTests() => _app = new EstoqueAppService(_repo, _relogio);

        [Fact]
        public async Task Adicionar_gera_id_e_calcula_disponivel()
        {
            var criado = await _app.AdicionarAsync(new EstoqueInputDto("Pastilha", TipoEstoque.Peca, 5, 2));

            Assert.Equal(1, criado.Id);
            Assert.Equal(5, criado.QuantidadeDisponivel);
            Assert.False(criado.BaixoEstoque);
            Assert.Equal(_relogio.UtcAgora, criado.DataUltimaAtualizacao);
        }

        [Fact]
        public async Task Adicionar_invalido_nao_consome_id()
        {
            await Assert.ThrowsAsync<RegraDeNegocioException>(() => _app.AdicionarAsync(new EstoqueInputDto(" ", TipoEstoque.Peca, 1, 0)));

            var criado = await _app.AdicionarAsync(new EstoqueInputDto("Oleo", TipoEstoque.Insumo, 1, 0));
            Assert.Equal(1, criado.Id);
        }

        [Fact]
        public async Task Listar_filtra_por_tipo()
        {
            await _app.AdicionarAsync(new EstoqueInputDto("Pastilha", TipoEstoque.Peca, 5, 2));
            await _app.AdicionarAsync(new EstoqueInputDto("Oleo", TipoEstoque.Insumo, 5, 2));

            Assert.Equal(2, (await _app.ListarAsync()).Count);
            Assert.Equal("Oleo", Assert.Single(await _app.ListarAsync(TipoEstoque.Insumo)).Nome);
        }

        [Fact]
        public async Task Obter_inexistente_lanca_nao_encontrado()
        {
            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => _app.ObterAsync(99));
        }

        [Fact]
        public async Task Atualizar_muda_cadastro_sem_mexer_no_saldo()
        {
            var criado = await _app.AdicionarAsync(new EstoqueInputDto("Pastilha", TipoEstoque.Peca, 5, 2));

            var atualizado = await _app.AtualizarAsync(criado.Id, new EstoqueCadastroDto("Pastilha dianteira", TipoEstoque.Peca, 6));

            Assert.Equal("Pastilha dianteira", atualizado.Nome);
            Assert.Equal(5, atualizado.QuantidadeAtual);
            Assert.True(atualizado.BaixoEstoque);
        }

        [Fact]
        public async Task Repor_soma_ao_saldo()
        {
            var criado = await _app.AdicionarAsync(new EstoqueInputDto("Pastilha", TipoEstoque.Peca, 5, 2));

            var reposto = await _app.ReporAsync(criado.Id, 3);

            Assert.Equal(8, reposto.QuantidadeAtual);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Repor_quantidade_invalida_lanca_regra(int quantidade)
        {
            var criado = await _app.AdicionarAsync(new EstoqueInputDto("Pastilha", TipoEstoque.Peca, 5, 2));

            await Assert.ThrowsAsync<RegraDeNegocioException>(() => _app.ReporAsync(criado.Id, quantidade));
        }

        [Fact]
        public async Task Remover_com_reserva_e_bloqueado()
        {
            var criado = await _app.AdicionarAsync(new EstoqueInputDto("Pastilha", TipoEstoque.Peca, 5, 2));
            await _repo.ReservarAsync([new ReservaEstoque(criado.Id, 1)], _relogio.UtcAgora);

            await Assert.ThrowsAsync<RegraDeNegocioException>(() => _app.RemoverAsync(criado.Id));
        }

        [Fact]
        public async Task Remover_sem_reserva_apaga()
        {
            var criado = await _app.AdicionarAsync(new EstoqueInputDto("Pastilha", TipoEstoque.Peca, 5, 2));

            await _app.RemoverAsync(criado.Id);

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => _app.ObterAsync(criado.Id));
        }
    }
}
