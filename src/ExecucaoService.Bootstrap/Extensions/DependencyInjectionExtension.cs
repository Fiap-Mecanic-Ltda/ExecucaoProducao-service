using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Infrastructure.Persistencia;
using ExecucaoService.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace ExecucaoService.Bootstrap.Extensions
{
    public static class DependencyInjectionExtension
    {
        public static IServiceCollection AddDependencyInjection(this IServiceCollection services)
        {
            services.AddSingleton<IRelogio, RelogioSistema>();

            services.AddScoped<IEstoqueRepository, EstoqueRepository>();
            services.AddScoped<IServicoOficinaRepository, ServicoOficinaRepository>();
            services.AddScoped<IExecucaoRepository, ExecucaoRepository>();

            services.AddScoped<IEstoqueAppService, EstoqueAppService>();
            services.AddScoped<IServicoOficinaAppService, ServicoOficinaAppService>();
            services.AddScoped<IExecucaoAppService, ExecucaoAppService>();

            services.AddHostedService<InicializadorDynamoDb>();

            return services;
        }
    }
}
