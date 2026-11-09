using ExecucaoService.API.Middlewares;

namespace ExecucaoService.API.Tests
{
    public class CorrelationIdTests : IClassFixture<ExecucaoServiceApiFactory>
    {
        private readonly HttpClient _client;

        public CorrelationIdTests(ExecucaoServiceApiFactory factory) => _client = factory.CreateClient();

        private async Task<string> CorrelationIdDaResposta(string? enviado)
        {
            var requisicao = new HttpRequestMessage(HttpMethod.Get, "/health");
            if (enviado is not null)
                requisicao.Headers.Add(CorrelationIdMiddleware.HeaderName, enviado);

            var resposta = await _client.SendAsync(requisicao);

            return Assert.Single(resposta.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        }

        [Fact]
        public async Task Devolve_o_id_recebido_do_gateway()
        {
            Assert.Equal("req-123", await CorrelationIdDaResposta("req-123"));
        }

        [Fact]
        public async Task Gera_um_id_quando_a_requisicao_nao_traz()
        {
            var gerado = await CorrelationIdDaResposta(null);

            Assert.Equal(32, gerado.Length);
        }

        [Fact]
        public async Task Troca_um_id_grande_demais_por_um_novo()
        {
            var gerado = await CorrelationIdDaResposta(new string('x', 500));

            Assert.Equal(32, gerado.Length);
        }
    }
}
