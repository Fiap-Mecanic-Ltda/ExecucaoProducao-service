using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Application.Options;
using ExecucaoService.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExecucaoService.Infrastructure.Automacao
{
    /// <summary>
    /// Demonstração (Execucao:ModoAutomatico=true): faz o papel do mecânico. A cada ciclo, toda OS
    /// parada há pelo menos <see cref="ExecucaoOptions.SegundosPorPasso"/> segundos avança um
    /// passo: inicia o diagnóstico, diagnostica, inicia o reparo e finaliza. Diagnóstico concluído
    /// espera o pagamento (IniciarExecucao) e não é tocado.
    /// </summary>
    public class ModoAutomaticoWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _escopos;
        private readonly ExecucaoOptions _opcoes;
        private readonly IRelogio _relogio;
        private readonly ILogger<ModoAutomaticoWorker> _logger;

        public ModoAutomaticoWorker(IServiceScopeFactory escopos, IOptions<ExecucaoOptions> opcoes, IRelogio relogio,
                                    ILogger<ModoAutomaticoWorker> logger)
        {
            _escopos = escopos;
            _opcoes  = opcoes.Value;
            _relogio = relogio;
            _logger  = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogWarning("Modo automático ligado: diagnóstico e reparo avançam sozinhos a cada {Segundos}s.", _opcoes.SegundosPorPasso);
            var intervalo = TimeSpan.FromSeconds(Math.Max(1, _opcoes.SegundosPorPasso));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await AvancarAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Falha no ciclo do modo automático.");
                }

                try
                {
                    await Task.Delay(intervalo, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>Um ciclo: cada OS elegível avança um passo. Público para os testes.</summary>
        public async Task AvancarAsync(CancellationToken ct)
        {
            using var escopo = _escopos.CreateScope();
            var app = escopo.ServiceProvider.GetRequiredService<IExecucaoAppService>();
            var limite = _relogio.UtcAgora.AddSeconds(-_opcoes.SegundosPorPasso);

            foreach (var item in await app.ListarFilaAsync(null, ct))
            {
                if (item.DataAlteracaoStatus > limite)
                    continue;

                try
                {
                    switch (item.Status)
                    {
                        case StatusExecucao.AguardandoDiagnostico:
                            await app.IniciarDiagnosticoAsync(item.OrdemServicoId, ct);
                            break;
                        case StatusExecucao.EmDiagnostico:
                            await app.DiagnosticarAutomaticamenteAsync(item.OrdemServicoId, _opcoes.ValorPecaAutomatico, ct);
                            break;
                        case StatusExecucao.NaFila:
                            await app.IniciarReparoAsync(item.OrdemServicoId, ct);
                            break;
                        case StatusExecucao.EmExecucao:
                            await app.FinalizarAsync(item.OrdemServicoId, "Reparo concluído no modo automático", ct);
                            break;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Uma OS com problema (ex.: alterada ao mesmo tempo pela API) não para as outras.
                    _logger.LogWarning(ex, "Modo automático não avançou a OS {OrdemServicoId} ({Status}).", item.OrdemServicoId, item.Status);
                }
            }
        }
    }
}
