using ExecucaoService.Application.Interfaces;
using ExecucaoService.Application.Mensageria;
using MassTransit;
using MechanicLtda.Contratos;

namespace ExecucaoService.Infrastructure.Mensageria
{
    /// <summary>
    /// Publica no exchange da mensagem (MechanicLtda.Contratos:&lt;Nome&gt;). Dentro de um
    /// consumidor, o IPublishEndpoint do escopo é o próprio ConsumeContext, e o outbox em
    /// memória segura a publicação até o consumidor terminar sem erro.
    /// </summary>
    public class PublicadorMassTransit : IPublicadorEventos
    {
        private readonly IPublishEndpoint _publish;

        public PublicadorMassTransit(IPublishEndpoint publish) => _publish = publish;

        public Task PublicarAsync<T>(T evento, CancellationToken ct = default) where T : MensagemSaga =>
            _publish.Publish(evento, contexto =>
            {
                contexto.CorrelationId = evento.CorrelationId;
                contexto.Headers.Set(ContextoCorrelacao.CabecalhoCorrelationId, evento.CorrelationId.ToString());

                var requisicao = ContextoCorrelacao.CorrelationIdRequisicao;
                if (!string.IsNullOrWhiteSpace(requisicao))
                    contexto.Headers.Set(ContextoCorrelacao.CabecalhoRequisicao, requisicao);
            }, ct);
    }

    /// <summary>Mensageria desligada: nada é publicado (e o serviço nem conecta no RabbitMQ).</summary>
    public class PublicadorDesligado : IPublicadorEventos
    {
        public Task PublicarAsync<T>(T evento, CancellationToken ct = default) where T : MensagemSaga => Task.CompletedTask;
    }
}
