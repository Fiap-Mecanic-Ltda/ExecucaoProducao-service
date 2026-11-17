using ExecucaoService.Domain.Enums;

namespace ExecucaoService.Domain.Entities
{
    /// <summary>Uma mudança de status da OS dentro da Execução.</summary>
    public sealed record HistoricoExecucao(
        StatusExecucao? StatusAnterior,
        StatusExecucao StatusNovo,
        DateTime DataHora,
        string? Observacao);
}
