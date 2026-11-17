using System.Text.Json.Serialization;

namespace ExecucaoService.Domain.Enums
{
    /// <summary>Tipo do item de estoque, com os mesmos valores do MechanicLtda (1 = Insumo, 2 = Peça).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TipoEstoque
    {
        Insumo = 1,
        Peca   = 2
    }
}
