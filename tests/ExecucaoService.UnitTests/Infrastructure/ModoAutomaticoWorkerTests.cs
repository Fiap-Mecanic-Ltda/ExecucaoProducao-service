using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Application.Options;
using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Infrastructure.Automacao;
using ExecucaoService.Testes.Compartilhado;
using MechanicLtda.Contratos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ExecucaoService.UnitTests.Infrastructure
{
    public class ModoAutomaticoWorkerTests
    {
        private readonly RelogioFixo _relogio = new();
        private readonly ExecucoesEmMemoria _execucoes = new();
        private readonly PublicadorEmMemoria _publicador = new();
        private readonly ServicosEmMemoria _servicos = new();
        private readonly ModoAutomaticoWorker _worker;
        private readonly IExecucaoAppService _app;

        public ModoAutomaticoWorkerTests()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IRelogio>(_relogio);
            services.AddSingleton<IExecucaoRepository>(_execucoes);
            services.AddSingleton<IEstoqueRepository>(new EstoqueEmMemoria());
            services.AddSingleton<IServicoOficinaRepository>(_servicos);
            services.AddSingleton<IPublicadorEventos>(_publicador);
            services.AddSingleton<IMonitoramentoService>(new MonitoramentoEmMemoria());
            services.AddScoped<IExecucaoAppService, ExecucaoAppService>();
            var provider = services.BuildServiceProvider();

            _servicos.SalvarAsync(ServicoOficina.Criar(1, "Revisao", null, 200m, _relogio.UtcAgora)).Wait();
            _app    = provider.GetRequiredService<IExecucaoAppService>();
            _worker = new ModoAutomaticoWorker(provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new ExecucaoOptions { ModoAutomatico = true, SegundosPorPasso = 3 }), _relogio,
                NullLogger<ModoAutomaticoWorker>.Instance);
        }

        private async Task<StatusExecucao> CicloAsync()
        {
            _relogio.Avancar(TimeSpan.FromSeconds(3));
            await _worker.AvancarAsync(CancellationToken.None);
            return (await _execucoes.ObterAsync(42))!.Status;
        }

        [Fact]
        public async Task Avanca_um_passo_por_ciclo_e_espera_o_pagamento()
        {
            await _app.ReceberAsync(42, Guid.NewGuid(), null, null, "Revisao geral");

            Assert.Equal(StatusExecucao.EmDiagnostico, await CicloAsync());
            Assert.Equal(StatusExecucao.DiagnosticoConcluido, await CicloAsync());
            Assert.Equal(StatusExecucao.DiagnosticoConcluido, await CicloAsync());

            await _app.LiberarParaExecucaoAsync(42);

            Assert.Equal(StatusExecucao.EmExecucao, await CicloAsync());
            Assert.Equal(StatusExecucao.Finalizada, await CicloAsync());
            Assert.Single(_publicador.DoTipo<ExecucaoFinalizada>());
        }

        [Fact]
        public async Task Nao_mexe_na_os_que_acabou_de_mudar()
        {
            await _app.ReceberAsync(42, Guid.NewGuid(), null, null, null);

            await _worker.AvancarAsync(CancellationToken.None);

            Assert.Equal(StatusExecucao.AguardandoDiagnostico, (await _execucoes.ObterAsync(42))!.Status);
        }
    }
}
