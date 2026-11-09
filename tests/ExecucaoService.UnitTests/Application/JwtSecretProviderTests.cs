using Microsoft.Extensions.Configuration;
using ExecucaoService.Application.Security;

namespace ExecucaoService.UnitTests.Application
{
    // A variável de ambiente JWT_SECRET_KEY tem prioridade; os testes rodam sem ela.
    public class JwtSecretProviderTests
    {
        private static IConfiguration Config(Dictionary<string, string?> valores) =>
            new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        [Fact]
        public void Usa_JWT_SECRET_KEY_da_configuracao()
        {
            if (Environment.GetEnvironmentVariable("JWT_SECRET_KEY") is not null) return;

            var config = Config(new() { ["JWT_SECRET_KEY"] = "chave-raiz", ["JwtSettings:SecretKey"] = "chave-secao" });

            Assert.Equal("chave-raiz", JwtSecretProvider.GetSecretKey(config));
        }

        [Fact]
        public void Usa_JwtSettings_SecretKey_quando_nao_ha_chave_na_raiz()
        {
            if (Environment.GetEnvironmentVariable("JWT_SECRET_KEY") is not null) return;

            var config = Config(new() { ["JwtSettings:SecretKey"] = "chave-secao" });

            Assert.Equal("chave-secao", JwtSecretProvider.GetSecretKey(config));
        }

        [Fact]
        public void Sem_chave_lanca_erro_explicando_o_que_configurar()
        {
            if (Environment.GetEnvironmentVariable("JWT_SECRET_KEY") is not null) return;

            var erro = Assert.Throws<InvalidOperationException>(() => JwtSecretProvider.GetSecretKey(Config(new())));

            Assert.Contains("JWT_SECRET_KEY", erro.Message);
        }
    }
}
