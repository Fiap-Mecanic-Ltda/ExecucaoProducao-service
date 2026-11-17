using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;

namespace ExecucaoService.Application.DTOs
{
    /// <summary>Entrada manual na fila, enquanto a Saga não envia IniciarDiagnostico.</summary>
    public sealed record EntradaManualDto(int OrdemServicoId, int? ClienteId, int? VeiculoId, string? DescricaoProblema);

    /// <summary>
    /// Item informado pelo mecânico. Peça e insumo exigem descrição e valor unitário (o estoque
    /// não guarda preço). Para um serviço, descrição e valor podem ser omitidos e vêm do catálogo.
    /// </summary>
    public sealed record ItemDiagnosticoDto(
        string? Descricao, TipoItem Tipo, int Quantidade, decimal? ValorUnitario, int? EstoqueId, int? ServicoOficinaId);

    public sealed record DiagnosticoDto(IReadOnlyList<ItemDiagnosticoDto> Itens, string? Observacao);

    public sealed record ItemExecucaoDto(
        string Descricao, TipoItem Tipo, int Quantidade, decimal ValorUnitario, decimal ValorTotal, int? EstoqueId, int? ServicoOficinaId)
    {
        public static ItemExecucaoDto De(ItemExecucao i) =>
            new(i.Descricao, i.Tipo, i.Quantidade, i.ValorUnitario, i.ValorTotal, i.EstoqueId, i.ServicoOficinaId);
    }

    public sealed record HistoricoExecucaoDto(StatusExecucao? StatusAnterior, StatusExecucao StatusNovo, DateTime DataHora, string? Observacao)
    {
        public static HistoricoExecucaoDto De(HistoricoExecucao h) => new(h.StatusAnterior, h.StatusNovo, h.DataHora, h.Observacao);
    }

    public sealed record ExecucaoDto(
        int OrdemServicoId, Guid CorrelationId, int? ClienteId, int? VeiculoId, string? DescricaoProblema,
        StatusExecucao Status, string? Observacao, string? MotivoCancelamento,
        DateTime DataEntrada, DateTime DataAlteracaoStatus, DateTime? DataConclusaoDiagnostico, DateTime? DataInicio, DateTime? DataFim,
        decimal ValorTotalItens, IReadOnlyList<ItemExecucaoDto> Itens, IReadOnlyList<HistoricoExecucaoDto> Historico)
    {
        public static ExecucaoDto De(ExecucaoOrdemServico e) =>
            new(e.OrdemServicoId, e.CorrelationId, e.ClienteId, e.VeiculoId, e.DescricaoProblema,
                e.Status, e.Observacao, e.MotivoCancelamento,
                e.DataEntrada, e.DataAlteracaoStatus, e.DataConclusaoDiagnostico, e.DataInicio, e.DataFim,
                e.ValorTotalItens,
                e.Itens.Select(ItemExecucaoDto.De).ToList(),
                e.Historico.Concat(e.HistoricoNaoGravado).Select(HistoricoExecucaoDto.De).ToList());
    }

    /// <summary>Resumo de uma OS na fila (sem itens nem histórico).</summary>
    public sealed record FilaItemDto(int OrdemServicoId, StatusExecucao Status, string? DescricaoProblema, DateTime DataEntrada, DateTime DataAlteracaoStatus)
    {
        public static FilaItemDto De(ExecucaoOrdemServico e) =>
            new(e.OrdemServicoId, e.Status, e.DescricaoProblema, e.DataEntrada, e.DataAlteracaoStatus);
    }
}
