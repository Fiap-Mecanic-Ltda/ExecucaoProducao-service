using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MassTransit;
using MassTransit.Testing;
using MechanicLtda.Contratos;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace ExecucaoService.API.Tests.Bdd
{
    /// <summary>
    /// Passos dos cenários BDD. A API sobe em memória com a mensageria ligada (transporte em
    /// memória e test harness do MassTransit): os passos "a Saga envia" publicam os comandos como
    /// o OS Service faria, e os passos "a Execução publica" conferem os eventos.
    /// </summary>
    [Binding]
    public class PassosExecucao
    {
        private static readonly Lazy<ApiComSagaFactory> Fabrica = new(() => new ApiComSagaFactory());
        private static readonly CultureInfo PtBr = new("pt-BR");

        private readonly Dictionary<string, int> _estoque = [];
        private readonly Dictionary<string, int> _servicos = [];
        private readonly Dictionary<int, Guid> _sagas = [];
        private HttpClient _cliente = null!;
        private HttpResponseMessage? _ultimaResposta;

        private static ITestHarness Harness => Fabrica.Value.Services.GetRequiredService<ITestHarness>();

        [AfterTestRun]
        public static void Encerrar()
        {
            if (Fabrica.IsValueCreated)
                Fabrica.Value.Dispose();
        }

        // ── Contexto ─────────────────────────────────────────────────────────

        [Given("que a mensageria da Saga está ligada")]
        public void DadoMensageriaLigada() => _ = Fabrica.Value.Services;

        [Given("que estou autenticado como funcionário")]
        public void DadoAutenticado() => _cliente = Fabrica.Value.CriarClienteAutenticado("Funcionario");

        [Given("um item de estoque {string} com {int} unidades")]
        public async Task DadoItemDeEstoque(string nome, int quantidade)
        {
            var resposta = await _cliente.PostAsJsonAsync("/api/estoque", new { nome, tipo = "Peca", quantidadeAtual = quantidade, quantidadeMinima = 0 });
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
            _estoque[nome] = (await Corpo(resposta)).GetProperty("id").GetInt32();
        }

        [Given(@"^o serviço ""(.*)"" de (\d+,\d{2}) no catálogo$")]
        public async Task DadoServico(string nome, string valor)
        {
            var resposta = await _cliente.PostAsJsonAsync("/api/servicos-oficina", new { nome, valorBase = Valor(valor) });
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
            _servicos[nome] = (await Corpo(resposta)).GetProperty("id").GetInt32();
        }

        // ── Comandos da Saga ─────────────────────────────────────────────────

        [When("a Saga envia IniciarDiagnostico para a OS {int} com o problema {string}")]
        public async Task QuandoIniciarDiagnostico(int os, string problema)
        {
            _sagas[os] = Guid.NewGuid();
            await PublicarAsync(new IniciarDiagnostico
            {
                CorrelationId = _sagas[os], OrdemServicoId = os, DataHora = DateTime.UtcNow,
                ClienteId = 1, VeiculoId = 10, DescricaoProblema = problema,
            });
        }

        [When("a Saga reenvia IniciarDiagnostico para a OS {int}")]
        public Task QuandoReenviaIniciarDiagnostico(int os) =>
            PublicarAsync(new IniciarDiagnostico { CorrelationId = _sagas[os], OrdemServicoId = os, DataHora = DateTime.UtcNow, ClienteId = 1, VeiculoId = 10 });

        [When("a Saga envia IniciarExecucao para a OS {int}")]
        public Task QuandoIniciarExecucao(int os) =>
            PublicarAsync(new IniciarExecucao { CorrelationId = _sagas[os], OrdemServicoId = os, DataHora = DateTime.UtcNow, OrcamentoId = 1000 + os });

        [When("a Saga envia LiberarReservaEstoque para a OS {int} com o motivo {string}")]
        public Task QuandoLiberarReserva(int os, string motivo) =>
            PublicarAsync(new LiberarReservaEstoque { CorrelationId = _sagas[os], OrdemServicoId = os, DataHora = DateTime.UtcNow, Motivo = motivo });

        // ── Mecânico ─────────────────────────────────────────────────────────

        [When(@"^o mecânico registra o diagnóstico da OS (\d+) com (\d+) ""(.*)"" a (\d+,\d{2}) e o serviço ""(.*)""$")]
        public async Task QuandoRegistraDiagnostico(int os, int quantidade, string peca, string valor, string servico)
        {
            await QuandoTentaRegistrarDiagnostico(os, quantidade, peca, valor, servico);
            Assert.Equal(HttpStatusCode.OK, _ultimaResposta!.StatusCode);
        }

        [When(@"^o mecânico tenta registrar o diagnóstico da OS (\d+) com (\d+) ""(.*)"" a (\d+,\d{2}) e o serviço ""(.*)""$")]
        public async Task QuandoTentaRegistrarDiagnostico(int os, int quantidade, string peca, string valor, string servico)
        {
            _ultimaResposta = await _cliente.PostAsJsonAsync($"/api/execucao/{os}/diagnostico", new
            {
                itens = new object[]
                {
                    new { tipo = "Peca", quantidade, valorUnitario = Valor(valor), estoqueId = _estoque[peca] },
                    new { tipo = "Servico", quantidade = 1, servicoOficinaId = _servicos[servico] },
                }
            });
        }

        [When("o mecânico registra a falha do diagnóstico da OS {int} com o motivo {string}")]
        public async Task QuandoRegistraFalha(int os, string motivo)
        {
            var resposta = await _cliente.PatchAsJsonAsync($"/api/execucao/{os}/falha-diagnostico", new { motivo });
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        }

        [When("o mecânico inicia e finaliza o reparo da OS {int}")]
        public async Task QuandoIniciaEFinaliza(int os)
        {
            Assert.Equal(HttpStatusCode.OK, (await _cliente.PatchAsync($"/api/execucao/{os}/iniciar", null)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await _cliente.PatchAsJsonAsync($"/api/execucao/{os}/finalizar", new { observacao = "Testado" })).StatusCode);
        }

        // ── Verificações ─────────────────────────────────────────────────────

        [Then("a API recusa com {string}")]
        public async Task EntaoApiRecusa(string trecho)
        {
            Assert.Equal(HttpStatusCode.BadRequest, _ultimaResposta!.StatusCode);
            Assert.Contains(trecho, await _ultimaResposta.Content.ReadAsStringAsync());
        }

        [Then("a OS {int} está {string} na fila de execução")]
        [Then("a OS {int} está {string} na Execução")]
        public async Task EntaoStatus(int os, string status)
        {
            // Os comandos são assíncronos: consulta até o status aparecer.
            var limite = DateTime.UtcNow.AddSeconds(10);
            string? atual = null;
            do
            {
                var resposta = await _cliente.GetAsync($"/api/execucao/{os}");
                if (resposta.IsSuccessStatusCode)
                    atual = (await Corpo(resposta)).GetProperty("status").GetString();
                if (atual == status)
                    return;
                await Task.Delay(100);
            } while (DateTime.UtcNow < limite);

            Assert.Equal(status, atual);
        }

        [Then("o item {string} tem {int} disponíveis e {int} reservadas")]
        public async Task EntaoDisponiveis(string nome, int disponiveis, int reservadas)
        {
            var item = await Corpo(await _cliente.GetAsync($"/api/estoque/{_estoque[nome]}"));
            Assert.Equal(disponiveis, item.GetProperty("quantidadeDisponivel").GetInt32());
            Assert.Equal(reservadas, item.GetProperty("quantidadeReservada").GetInt32());
        }

        [Then("o item {string} tem {int} unidades e {int} reservadas")]
        public async Task EntaoUnidades(string nome, int unidades, int reservadas)
        {
            var item = await Corpo(await _cliente.GetAsync($"/api/estoque/{_estoque[nome]}"));
            Assert.Equal(unidades, item.GetProperty("quantidadeAtual").GetInt32());
            Assert.Equal(reservadas, item.GetProperty("quantidadeReservada").GetInt32());
        }

        [Then(@"^a Execução publica DiagnosticoConcluido da OS (\d+) com o valor total (\d+,\d{2})$")]
        public async Task EntaoDiagnosticoConcluido(int os, string total)
        {
            var evento = await PublicadoAsync<DiagnosticoConcluido>(os);
            Assert.Equal(_sagas[os], evento.CorrelationId);
            Assert.Equal(Valor(total), evento.Itens.Sum(i => i.Quantidade * i.ValorUnitario));
        }

        [Then("a Execução publica ExecucaoIniciada e ExecucaoFinalizada da OS {int}")]
        public async Task EntaoExecucaoIniciadaEFinalizada(int os)
        {
            var iniciada   = await PublicadoAsync<ExecucaoIniciada>(os);
            var finalizada = await PublicadoAsync<ExecucaoFinalizada>(os);
            Assert.Equal(iniciada.DataInicio, finalizada.DataInicio);
            Assert.True(finalizada.DataFim >= finalizada.DataInicio);
        }

        [Then("a Execução publica DiagnosticoFalhou da OS {int} com o motivo {string}")]
        public async Task EntaoDiagnosticoFalhou(int os, string motivo) =>
            Assert.Equal(motivo, (await PublicadoAsync<DiagnosticoFalhou>(os)).Motivo);

        [Then("a Execução publica ExecucaoFalhou da OS {int}")]
        public async Task EntaoExecucaoFalhou(int os) => Assert.Equal(_sagas[os], (await PublicadoAsync<ExecucaoFalhou>(os)).CorrelationId);

        [Then("a Execução não publica falha para a OS {int}")]
        public void EntaoSemFalha(int os)
        {
            Assert.DoesNotContain(Harness.Published.Select<DiagnosticoFalhou>(), p => p.Context.Message.OrdemServicoId == os);
            Assert.DoesNotContain(Harness.Published.Select<ExecucaoFalhou>(), p => p.Context.Message.OrdemServicoId == os);
        }

        // ── Apoio ────────────────────────────────────────────────────────────

        private static async Task PublicarAsync<T>(T comando) where T : MensagemSaga
        {
            await Fabrica.Value.Services.GetRequiredService<IBus>().Publish(comando);
            foreach (var c in Harness.Consumed.Select<T>()) Console.WriteLine($"DEBUG c {c.Context.Message.DataHora:O} vs {comando.DataHora:O} {c.Exception?.Message}");

            // O harness só espera até o barramento ficar ocioso; o consumidor grava no estoque e na
            // OS, então a espera é feita aqui, com prazo.
            var limite = DateTime.UtcNow.AddSeconds(10);
            IReceivedMessage<T>? consumida;
            while ((consumida = Harness.Consumed.Select<T>(c => c.Context.Message.DataHora == comando.DataHora
                                                              && c.Context.Message.OrdemServicoId == comando.OrdemServicoId).FirstOrDefault()) is null
                   && DateTime.UtcNow < limite)
                await Task.Delay(50);

            Assert.True(consumida is not null, $"{typeof(T).Name} da OS {comando.OrdemServicoId} não foi consumido.");
            Assert.Null(consumida!.Exception);
        }

        private static async Task<T> PublicadoAsync<T>(int os) where T : MensagemSaga
        {
            var limite = DateTime.UtcNow.AddSeconds(10);
            IPublishedMessage<T>? publicada;
            while ((publicada = Harness.Published.Select<T>(p => p.Context.Message.OrdemServicoId == os).LastOrDefault()) is null
                   && DateTime.UtcNow < limite)
                await Task.Delay(50);

            Assert.True(publicada is not null, $"{typeof(T).Name} da OS {os} não foi publicado.");
            return publicada!.Context.Message;
        }

        private static decimal Valor(string texto) => decimal.Parse(texto, PtBr);

        private static async Task<JsonElement> Corpo(HttpResponseMessage resposta) => await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }
}
