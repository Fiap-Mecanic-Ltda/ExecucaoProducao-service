using MechanicLtda.Contratos;

namespace ExecucaoService.Application.Interfaces
{
    /// <summary>
    /// Publica os eventos da Execução para a Saga do OS Service, no exchange de cada mensagem,
    /// com os cabeçalhos <c>correlation-id</c> e <c>x-correlation-id</c>. Com a mensageria
    /// desligada, não faz nada.
    /// </summary>
    public interface IPublicadorEventos
    {
        Task PublicarAsync<T>(T evento, CancellationToken ct = default) where T : MensagemSaga;
    }
}
