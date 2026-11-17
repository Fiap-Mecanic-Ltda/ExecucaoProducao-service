namespace ExecucaoService.Domain.Exceptions
{
    /// <summary>OS, item de estoque ou serviço inexistente. Vira HTTP 404.</summary>
    public class RecursoNaoEncontradoException : Exception
    {
        public RecursoNaoEncontradoException(string mensagem) : base(mensagem) { }
    }
}
