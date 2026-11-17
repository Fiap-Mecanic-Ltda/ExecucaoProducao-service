using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Application.Options;
using ExecucaoService.Infrastructure.Automacao;
using ExecucaoService.Infrastructure.Monitoramento;
using ExecucaoService.Infrastructure.Persistencia;
using ExecucaoService.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExecucaoService.Bootstrap.Extensions
{
    public static class DependencyInjectionExtension
    {
        public static IServiceCollection AddDependencyInjection(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IRelogio, RelogioSistema>();
            services.AddSingleton<IMonitoramentoService, NewRelicMonitoramentoService>();
            services.Configure<ExecucaoOptions>(configuration.GetSection(ExecucaoOptions.Secao));

            services.AddScoped<IEstoqueRepository, EstoqueRepository>();
            services.AddScoped<IServicoOficinaRepository, ServicoOficinaRepository>();
            services.AddScoped<IExecucaoRepository, ExecucaoRepository>();
            services.AddScoped<IRegistroMensagens, RegistroMensagensRepository>();

            services.AddScoped<IEstoqueAppService, EstoqueAppService>();
            services.AddScoped<IServicoOficinaAppService, ServicoOficinaAppService>();
            services.AddScoped<IExecucaoAppService, ExecucaoAppService>();
            services.AddScoped<IComandosSagaAppService, ComandosSagaAppService>();

            services.AddHostedService<InicializadorDynamoDb>();

            if (configuration.GetSection(ExecucaoOptions.Secao).GetValue<bool>(nameof(ExecucaoOptions.ModoAutomatico)))
                services.AddHostedService<ModoAutomaticoWorker>();

            return services;
        }
    }
}
