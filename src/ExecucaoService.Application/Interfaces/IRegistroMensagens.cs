using MechanicLtda.Contratos;

namespace ExecucaoService.Application.Interfaces
{
    /// <summary>
    /// Mensagens da Saga já tratadas (tabela execucao-mensagens), com a resposta publicada, para
    /// que uma entrega repetida do RabbitMQ (at-least-once) republique a mesma resposta em vez de
    /// processar de novo (ADR-003).
    /// </summary>
    public interface IRegistroMensagens
    {
        /// <summary>Null se a mensagem ainda não foi tratada.</summary>
        Task<MensagemTratada?> ObterAsync(string chave, CancellationToken ct = default);

        Task RegistrarAsync(string chave, MensagemSaga? resposta, DateTime agora, CancellationToken ct = default);
    }

    /// <summary>Uma mensagem tratada e a resposta que ela gerou (null quando não houve resposta).</summary>
    public sealed record MensagemTratada(string Chave, MensagemSaga? Resposta);
}
