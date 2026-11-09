using MassTransit;
using Microsoft.AspNetCore.Hosting;

namespace ExecucaoService.API.Tests
{
    /// <summary>
    /// API com repositórios em memória e a mensageria ligada no transporte em memória, com o test
    /// harness do MassTransit: os testes publicam os comandos da Saga e conferem os eventos.
    /// </summary>
    public class ApiComSagaFactory : ApiComRepositoriosEmMemoriaFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            // UseSetting, e não ConfigureAppConfiguration: o Program lê a seção Mensageria ao
            // registrar os serviços, antes de os callbacks de configuração da fábrica rodarem.
            builder.UseSetting("Mensageria:Habilitada", "true");
            builder.UseSetting("Mensageria:Transporte", "InMemory");
            builder.ConfigureServices(services => services.AddMassTransitTestHarness());
        }
    }
}
