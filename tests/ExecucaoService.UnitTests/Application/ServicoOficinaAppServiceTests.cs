using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.DTOs;
using ExecucaoService.Domain.Exceptions;
using ExecucaoService.Testes.Compartilhado;

namespace ExecucaoService.UnitTests.Application
{
    public class ServicoOficinaAppServiceTests
    {
        private readonly ServicosEmMemoria _repo = new();
        private readonly ServicoOficinaAppService _app;

        public ServicoOficinaAppServiceTests() => _app = new ServicoOficinaAppService(_repo, new RelogioFixo());

        [Fact]
        public async Task Adicionar_inativo_ja_grava_inativo()
        {
            var criado = await _app.AdicionarAsync(new ServicoOficinaInputDto("Alinhamento", null, 80m, Ativo: false));

            Assert.False(criado.Ativo);
            Assert.Empty(await _app.ListarAsync(somenteAtivos: true));
        }

        [Fact]
        public async Task Listar_ordena_por_nome()
        {
            await _app.AdicionarAsync(new ServicoOficinaInputDto("Troca de oleo", null, 60m));
            await _app.AdicionarAsync(new ServicoOficinaInputDto("Alinhamento", null, 80m));

            Assert.Equal(["Alinhamento", "Troca de oleo"], (await _app.ListarAsync()).Select(s => s.Nome));
        }

        [Fact]
        public async Task Atualizar_e_remover()
        {
            var criado = await _app.AdicionarAsync(new ServicoOficinaInputDto("Alinhamento", null, 80m));

            var atualizado = await _app.AtualizarAsync(criado.Id, new ServicoOficinaInputDto("Alinhamento 3D", "Com balanceamento", 99.999m, false));
            Assert.Equal("Alinhamento 3D", atualizado.Nome);
            Assert.Equal(100m, atualizado.ValorBase);
            Assert.False(atualizado.Ativo);

            await _app.RemoverAsync(criado.Id);
            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => _app.ObterAsync(criado.Id));
        }

        [Fact]
        public async Task Valor_negativo_lanca_regra()
        {
            await Assert.ThrowsAsync<RegraDeNegocioException>(() => _app.AdicionarAsync(new ServicoOficinaInputDto("X", null, -1m)));
        }

        [Fact]
        public async Task Remover_inexistente_lanca_nao_encontrado()
        {
            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => _app.RemoverAsync(5));
        }
    }
}
