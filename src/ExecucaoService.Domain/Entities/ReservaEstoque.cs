namespace ExecucaoService.Domain.Entities
{
    /// <summary>Quantidade de um item de estoque presa a uma OS entre o diagnóstico e a execução.</summary>
    public sealed record ReservaEstoque(int EstoqueId, int Quantidade);
}
