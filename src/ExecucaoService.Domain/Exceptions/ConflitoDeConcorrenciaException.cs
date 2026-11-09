namespace ExecucaoService.Domain.Exceptions
{
    /// <summary>
    /// O registro mudou entre a leitura e a gravação (outra requisição ou mensagem venceu).
    /// Vira HTTP 409; nos consumidores, o retry do MassTransit tenta de novo.
    /// </summary>
    public class ConflitoDeConcorrenciaException : Exception
    {
        public ConflitoDeConcorrenciaException(string mensagem) : base(mensagem) { }
    }
}
