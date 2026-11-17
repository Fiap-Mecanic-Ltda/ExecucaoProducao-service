using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.Mensageria;
using MassTransit;
using MechanicLtda.Contratos;
using Microsoft.Extensions.Logging;

namespace ExecucaoService.Infrastructure.Mensageria
{
    /// <summary>
    /// Filas da Execução: execucao-&lt;comando-em-kebab&gt;, cada uma ligada ao exchange do comando.
    /// </summary>
    public static class FilasExecucao
    {
        public const string IniciarDiagnostico    = "execucao-iniciar-diagnostico";
        public const string IniciarExecucao       = "execucao-iniciar-execucao";
        public const string LiberarReservaEstoque = "execucao-liberar-reserva-estoque";

        public static readonly IReadOnlySet<string> Todas = new HashSet<string> { IniciarDiagnostico, IniciarExecucao, LiberarReservaEstoque };
    }

    /// <summary>Base dos consumidores: leva o x-correlation-id da mensagem para os logs e para os eventos publicados.</summary>
    public abstract class ConsumidorSaga<T> : IConsumer<T> where T : MensagemSaga
    {
        private readonly ILogger _logger;

        protected ConsumidorSaga(ILogger logger) => _logger = logger;

        public async Task Consume(ConsumeContext<T> context)
        {
            var requisicao = context.Headers.Get<string>(ContextoCorrelacao.CabecalhoRequisicao);
            ContextoCorrelacao.CorrelationIdRequisicao = requisicao;

            using (_logger.BeginScope(new Dictionary<string, object?>
                   {
                       ["CorrelationId"]  = requisicao,
                       ["SagaId"]         = context.Message.CorrelationId,
                       ["OrdemServicoId"] = context.Message.OrdemServicoId,
                   }))
            {
                _logger.LogInformation("{Mensagem} recebido para a OS {OrdemServicoId}.", typeof(T).Name, context.Message.OrdemServicoId);
                await Tratar(context.Message, context.CancellationToken);
            }
        }

        protected abstract Task Tratar(T mensagem, CancellationToken ct);
    }

    public class IniciarDiagnosticoConsumer : ConsumidorSaga<IniciarDiagnostico>
    {
        private readonly IComandosSagaAppService _app;

        public IniciarDiagnosticoConsumer(IComandosSagaAppService app, ILogger<IniciarDiagnosticoConsumer> logger) : base(logger) => _app = app;

        protected override Task Tratar(IniciarDiagnostico mensagem, CancellationToken ct) => _app.TratarAsync(mensagem, ct);
    }

    public class IniciarExecucaoConsumer : ConsumidorSaga<IniciarExecucao>
    {
        private readonly IComandosSagaAppService _app;

        public IniciarExecucaoConsumer(IComandosSagaAppService app, ILogger<IniciarExecucaoConsumer> logger) : base(logger) => _app = app;

        protected override Task Tratar(IniciarExecucao mensagem, CancellationToken ct) => _app.TratarAsync(mensagem, ct);
    }

    public class LiberarReservaEstoqueConsumer : ConsumidorSaga<LiberarReservaEstoque>
    {
        private readonly IComandosSagaAppService _app;

        public LiberarReservaEstoqueConsumer(IComandosSagaAppService app, ILogger<LiberarReservaEstoqueConsumer> logger) : base(logger) => _app = app;

        protected override Task Tratar(LiberarReservaEstoque mensagem, CancellationToken ct) => _app.TratarAsync(mensagem, ct);
    }
}
