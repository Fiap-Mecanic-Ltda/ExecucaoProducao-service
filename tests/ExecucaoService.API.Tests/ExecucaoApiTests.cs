using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ExecucaoService.API.Tests
{
    public class ExecucaoApiTests : IClassFixture<ApiComRepositoriosEmMemoriaFactory>
    {
        private readonly HttpClient _client;

        public ExecucaoApiTests(ApiComRepositoriosEmMemoriaFactory factory) => _client = factory.CriarClienteAutenticado("Funcionario");

        private async Task<int> CriarEstoqueAsync(int quantidade)
        {
            var resposta = await _client.PostAsJsonAsync("/api/estoque", new { nome = "Pastilha", tipo = "Peca", quantidadeAtual = quantidade, quantidadeMinima = 0 });
            return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        }

        private async Task<int> CriarServicoAsync()
        {
            var resposta = await _client.PostAsJsonAsync("/api/servicos-oficina", new { nome = "Troca de pastilhas", valorBase = 120m });
            return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        }

        private static async Task<JsonElement> Corpo(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

        [Fact]
        public async Task Fluxo_manual_do_diagnostico_ate_a_finalizacao()
        {
            const int os = 501;
            var pastilha = await CriarEstoqueAsync(4);
            var servico  = await CriarServicoAsync();

            var entrada = await _client.PostAsJsonAsync("/api/execucao", new { ordemServicoId = os, descricaoProblema = "Freio chiando" });
            Assert.Equal(HttpStatusCode.Created, entrada.StatusCode);
            Assert.Equal("AguardandoDiagnostico", (await Corpo(entrada)).GetProperty("status").GetString());

            Assert.Equal(HttpStatusCode.OK, (await _client.PatchAsync($"/api/execucao/{os}/iniciar-diagnostico", null)).StatusCode);

            var diagnostico = await _client.PostAsJsonAsync($"/api/execucao/{os}/diagnostico", new
            {
                itens = new object[]
                {
                    new { tipo = "Peca", quantidade = 2, valorUnitario = 89.9m, estoqueId = pastilha },
                    new { tipo = "Servico", quantidade = 1, servicoOficinaId = servico },
                },
                observacao = "Discos ok"
            });
            Assert.Equal(HttpStatusCode.OK, diagnostico.StatusCode);
            Assert.Equal(299.8m, (await Corpo(diagnostico)).GetProperty("valorTotalItens").GetDecimal());

            var estoque = await Corpo(await _client.GetAsync($"/api/estoque/{pastilha}"));
            Assert.Equal(2, estoque.GetProperty("quantidadeReservada").GetInt32());

            var fila = await _client.GetFromJsonAsync<JsonElement>("/api/execucao/fila?status=DiagnosticoConcluido");
            Assert.Contains(fila.EnumerateArray(), f => f.GetProperty("ordemServicoId").GetInt32() == os);

            Assert.Equal(HttpStatusCode.OK, (await _client.PatchAsync($"/api/execucao/{os}/liberar-execucao", null)).StatusCode);
            estoque = await Corpo(await _client.GetAsync($"/api/estoque/{pastilha}"));
            Assert.Equal(2, estoque.GetProperty("quantidadeAtual").GetInt32());
            Assert.Equal(0, estoque.GetProperty("quantidadeReservada").GetInt32());

            Assert.Equal(HttpStatusCode.OK, (await _client.PatchAsync($"/api/execucao/{os}/iniciar", null)).StatusCode);
            var final = await _client.PatchAsJsonAsync($"/api/execucao/{os}/finalizar", new { observacao = "Testado" });
            Assert.Equal("Finalizada", (await Corpo(final)).GetProperty("status").GetString());

            var cancelar = await _client.PatchAsJsonAsync($"/api/execucao/{os}/cancelar", new { motivo = "tarde demais" });
            Assert.Equal(HttpStatusCode.BadRequest, cancelar.StatusCode);
        }

        [Fact]
        public async Task Diagnostico_sem_saldo_responde_400_e_nao_reserva()
        {
            const int os = 502;
            var pastilha = await CriarEstoqueAsync(1);
            await _client.PostAsJsonAsync("/api/execucao", new { ordemServicoId = os });

            var resposta = await _client.PostAsJsonAsync($"/api/execucao/{os}/diagnostico", new
            {
                itens = new[] { new { tipo = "Peca", quantidade = 2, valorUnitario = 10m, estoqueId = pastilha } }
            });

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            var estoque = await Corpo(await _client.GetAsync($"/api/estoque/{pastilha}"));
            Assert.Equal(0, estoque.GetProperty("quantidadeReservada").GetInt32());
        }

        [Fact]
        public async Task Cancelar_devolve_as_reservas()
        {
            const int os = 503;
            var pastilha = await CriarEstoqueAsync(3);
            await _client.PostAsJsonAsync("/api/execucao", new { ordemServicoId = os });
            await _client.PostAsJsonAsync($"/api/execucao/{os}/diagnostico", new
            {
                itens = new[] { new { tipo = "Peca", quantidade = 3, valorUnitario = 10m, estoqueId = pastilha } }
            });

            var cancelada = await _client.PatchAsJsonAsync($"/api/execucao/{os}/cancelar", new { motivo = "Orcamento recusado" });

            Assert.Equal("Cancelada", (await Corpo(cancelada)).GetProperty("status").GetString());
            var estoque = await Corpo(await _client.GetAsync($"/api/estoque/{pastilha}"));
            Assert.Equal(3, estoque.GetProperty("quantidadeDisponivel").GetInt32());
        }

        [Fact]
        public async Task Os_fora_da_execucao_responde_404()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/execucao/9999")).StatusCode);
        }

        [Fact]
        public async Task Diagnostico_sem_itens_responde_400()
        {
            var resposta = await _client.PostAsJsonAsync("/api/execucao/504/diagnostico", new { itens = Array.Empty<object>() });

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        }
    
        [Fact]
        public async Task Falha_de_diagnostico_cancela_a_os()
        {
            const int os = 505;
            await _client.PostAsJsonAsync("/api/execucao", new { ordemServicoId = os });

            Assert.Equal(HttpStatusCode.BadRequest, (await _client.PatchAsJsonAsync($"/api/execucao/{os}/falha-diagnostico", new { motivo = "" })).StatusCode);

            var resposta = await _client.PatchAsJsonAsync($"/api/execucao/{os}/falha-diagnostico", new { motivo = "Peca fora de linha" });

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            Assert.Equal("Cancelada", (await Corpo(resposta)).GetProperty("status").GetString());
        }
    }
}
