using System.Text.Json.Serialization;

namespace ExecucaoService.Domain.Enums
{
    /// <summary>Tipo do item do diagnóstico, com os mesmos nomes do contrato de mensagens.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TipoItem
    {
        Peca    = 1,
        Insumo  = 2,
        Servico = 3
    }
}
