using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ExecucaoService.API.Tests
{
    public class EstoqueEServicosApiTests : IClassFixture<ApiComRepositoriosEmMemoriaFactory>
    {
        private readonly ApiComRepositoriosEmMemoriaFactory _factory;
        private readonly HttpClient _client;

        public EstoqueEServicosApiTests(ApiComRepositoriosEmMemoriaFactory factory)
        {
            _factory = factory;
            _client  = factory.CriarClienteAutenticado();
        }

        [Fact]
        public async Task Sem_token_responde_401()
        {
            var resposta = await _factory.CreateClient().GetAsync("/api/estoque");

            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        }

        [Fact]
        public async Task Cliente_nao_acessa_o_estoque()
        {
            var resposta = await _factory.CriarClienteAutenticado("Cliente").GetAsync("/api/estoque");

            Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        }

        [Fact]
        public async Task Cadastra_repoe_e_remove_item_de_estoque()
        {
            var criado = await _client.PostAsJsonAsync("/api/estoque", new { nome = "Filtro de ar", tipo = "Peca", quantidadeAtual = 2, quantidadeMinima = 1 });
            Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
            var id = (await criado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            var reposto = await _client.PatchAsJsonAsync($"/api/estoque/{id}/reposicao", new { quantidade = 3 });
            Assert.Equal(HttpStatusCode.OK, reposto.StatusCode);
            Assert.Equal(5, (await reposto.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("quantidadeAtual").GetInt32());

            var porTipo = await _client.GetFromJsonAsync<JsonElement>("/api/estoque/tipo/Peca");
            Assert.Contains(porTipo.EnumerateArray(), e => e.GetProperty("id").GetInt32() == id);

            Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/estoque/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/estoque/{id}")).StatusCode);
        }

        [Fact]
        public async Task Item_inexistente_responde_404_no_envelope_de_erro()
        {
            var resposta = await _client.GetAsync("/api/estoque/9999");

            Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
            var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(corpo.GetProperty("success").GetBoolean());
            Assert.NotEmpty(corpo.GetProperty("errors").EnumerateArray());
        }

        [Fact]
        public async Task Reposicao_com_quantidade_zero_responde_400()
        {
            var resposta = await _client.PatchAsJsonAsync("/api/estoque/1/reposicao", new { quantidade = 0 });

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        }

        [Fact]
        public async Task Cadastra_servico_e_lista_os_ativos()
        {
            var criado = await _client.PostAsJsonAsync("/api/servicos-oficina", new { nome = "Balanceamento", valorBase = 50m });
            Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
            var id = (await criado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            var ativos = await _client.GetFromJsonAsync<JsonElement>("/api/servicos-oficina/ativos");
            Assert.Contains(ativos.EnumerateArray(), s => s.GetProperty("id").GetInt32() == id);

            var inativado = await _client.PutAsJsonAsync($"/api/servicos-oficina/{id}", new { nome = "Balanceamento", valorBase = 50m, ativo = false });
            Assert.Equal(HttpStatusCode.OK, inativado.StatusCode);

            ativos = await _client.GetFromJsonAsync<JsonElement>("/api/servicos-oficina/ativos");
            Assert.DoesNotContain(ativos.EnumerateArray(), s => s.GetProperty("id").GetInt32() == id);
        }
    }
}
