using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Application.Mensageria;
using ExecucaoService.Bootstrap.Extensions;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Infrastructure.Mensageria;
using ExecucaoService.Testes.Compartilhado;
using MassTransit;
using MassTransit.Testing;
using MechanicLtda.Contratos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExecucaoService.UnitTests.Infrastructure
{
    /// <summary>Consumidores e publicação pelo MassTransit, com o transporte em memória do test harness.</summary>
    public class ConsumidoresSagaTests : IAsyncLifetime
    {
        private static readonly Guid Saga = Guid.Parse("3f0c2f6e-8a4b-4c1e-9a52-0b6f1d2e7c11");

        private readonly ExecucoesEmMemoria _execucoes = new();
        private readonly MonitoramentoEmMemoria _monitoramento = new();
        private ServiceProvider _provider = null!;
        private ITestHarness _harness = null!;

        public async Task InitializeAsync()
        {
            var configuracao = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Mensageria:Habilitada"] = "true",
                    ["Mensageria:Transporte"] = "InMemory",
                })
                .Build();

            var services = new ServiceCollection().AddLogging();
            services.AddSingleton<IRelogio>(new RelogioFixo());
            services.AddSingleton<IExecucaoRepository>(_execucoes);
            services.AddSingleton<IEstoqueRepository>(new EstoqueEmMemoria());
            services.AddSingleton<IServicoOficinaRepository>(new ServicosEmMemoria());
            services.AddSingleton<IRegistroMensagens>(new RegistroMensagensEmMemoria());
            services.AddSingleton<IMonitoramentoService>(_monitoramento);
            services.AddScoped<IExecucaoAppService, ExecucaoAppService>();
            services.AddScoped<IComandosSagaAppService, ComandosSagaAppService>();
            services.AddMensageria(configuracao);
            services.AddMassTransitTestHarness();

            _provider = services.BuildServiceProvider(true);
            _harness  = _provider.GetRequiredService<ITestHarness>();
            await _harness.Start();
        }

        public async Task DisposeAsync()
        {
            await _harness.Stop();
            await _provider.DisposeAsync();
        }

        [Fact]
        public async Task IniciarDiagnostico_publicado_pelo_os_service_entra_na_fila()
        {
            await _harness.Bus.Publish(new IniciarDiagnostico
            {
                CorrelationId = Saga, OrdemServicoId = 42, DataHora = DateTime.UtcNow, ClienteId = 7, VeiculoId = 3, DescricaoProblema = "Freio",
            });

            Assert.True(await _harness.GetConsumerHarness<IniciarDiagnosticoConsumer>().Consumed.Any<IniciarDiagnostico>());
            var execucao = await _execucoes.ObterAsync(42);
            Assert.Equal(StatusExecucao.AguardandoDiagnostico, execucao!.Status);
            Assert.Equal(Saga, execucao.CorrelationId);
        }

        [Fact]
        public async Task Falha_e_publicada_com_os_cabecalhos_de_correlacao()
        {
            await _harness.Bus.Publish(new IniciarExecucao { CorrelationId = Saga, OrdemServicoId = 77, DataHora = DateTime.UtcNow, OrcamentoId = 1 },
                                       c => c.Headers.Set(ContextoCorrelacao.CabecalhoRequisicao, "req-123"));

            Assert.True(await _harness.Published.Any<ExecucaoFalhou>());
            var publicada = _harness.Published.Select<ExecucaoFalhou>().First();
            Assert.Equal(Saga, publicada.Context.Message.CorrelationId);
            Assert.Equal(77, publicada.Context.Message.OrdemServicoId);
            Assert.Equal(Saga, publicada.Context.CorrelationId);
            Assert.Equal(Saga.ToString(), publicada.Context.Headers.Get<string>(ContextoCorrelacao.CabecalhoCorrelationId));
            Assert.Equal("req-123", publicada.Context.Headers.Get<string>(ContextoCorrelacao.CabecalhoRequisicao));
        }

        [Fact]
        public async Task LiberarReservaEstoque_de_os_desconhecida_e_consumido_sem_erro()
        {
            await _harness.Bus.Publish(new LiberarReservaEstoque { CorrelationId = Saga, OrdemServicoId = 5, DataHora = DateTime.UtcNow, Motivo = "Recusado" });

            var consumidor = _harness.GetConsumerHarness<LiberarReservaEstoqueConsumer>();
            Assert.True(await consumidor.Consumed.Any<LiberarReservaEstoque>());
            Assert.False(await _harness.Published.Any<Fault<LiberarReservaEstoque>>());
        }

        [Fact]
        public void Filas_seguem_o_padrao_do_contrato()
        {
            Assert.Equal(["execucao-iniciar-diagnostico", "execucao-iniciar-execucao", "execucao-liberar-reserva-estoque"],
                         FilasExecucao.Todas.Order());
        }
    }
}
