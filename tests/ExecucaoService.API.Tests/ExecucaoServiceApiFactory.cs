using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ExecucaoService.API.Tests
{
    /// <summary>
    /// Sobe a API em memória, com uma chave JWT de teste. Nenhum teste desta etapa abre
    /// conexão com o banco, então a connection string do appsettings não é usada.
    /// </summary>
    public class ExecucaoServiceApiFactory : WebApplicationFactory<Program>
    {
        public const string ChaveJwtTeste = "chave-jwt-de-teste-com-pelo-menos-32-caracteres";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT_SECRET_KEY"] = ChaveJwtTeste
                }));
        }
    }
}
