using System.Net;

namespace ExecucaoService.API.Tests
{
    public class HealthTests : IClassFixture<ExecucaoServiceApiFactory>
    {
        private readonly HttpClient _client;

        public HealthTests(ExecucaoServiceApiFactory factory) => _client = factory.CreateClient();

        [Fact]
        public async Task Health_responde_200_sem_token()
        {
            var resposta = await _client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            Assert.Contains("execucao-service", await resposta.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Swagger_publica_o_documento_da_api()
        {
            var resposta = await _client.GetAsync("/swagger/v1/swagger.json");

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            Assert.Contains("Execucao Service API", await resposta.Content.ReadAsStringAsync());
        }
    }
}
