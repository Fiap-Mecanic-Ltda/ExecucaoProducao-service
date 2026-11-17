using Amazon.DynamoDBv2.Model;
using System.Globalization;

namespace ExecucaoService.Infrastructure.Persistencia
{
    /// <summary>Conversões entre tipos .NET e AttributeValue, sempre em cultura invariável e UTC.</summary>
    internal static class Atributos
    {
        public const string Pk = "pk";
        public const string Sk = "sk";

        public static AttributeValue S(string valor) => new() { S = valor };

        public static AttributeValue N(int valor) => new() { N = valor.ToString(CultureInfo.InvariantCulture) };

        public static AttributeValue N(decimal valor) => new() { N = valor.ToString(CultureInfo.InvariantCulture) };

        public static AttributeValue Bool(bool valor) => new() { BOOL = valor };

        public static AttributeValue Data(DateTime valor) =>
            new() { S = DateTime.SpecifyKind(valor, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture) };

        public static void Opcional(Dictionary<string, AttributeValue> item, string nome, string? valor)
        {
            if (!string.IsNullOrEmpty(valor))
                item[nome] = S(valor);
        }

        public static void Opcional(Dictionary<string, AttributeValue> item, string nome, int? valor)
        {
            if (valor is not null)
                item[nome] = N(valor.Value);
        }

        public static void Opcional(Dictionary<string, AttributeValue> item, string nome, DateTime? valor)
        {
            if (valor is not null)
                item[nome] = Data(valor.Value);
        }

        public static string LerS(this Dictionary<string, AttributeValue> item, string nome) =>
            item.TryGetValue(nome, out var v) && v.S is not null ? v.S : throw new InvalidOperationException($"Atributo '{nome}' ausente.");

        public static string? LerSOpcional(this Dictionary<string, AttributeValue> item, string nome) =>
            item.TryGetValue(nome, out var v) ? v.S : null;

        public static int LerInt(this Dictionary<string, AttributeValue> item, string nome) =>
            int.Parse(item.TryGetValue(nome, out var v) && v.N is not null ? v.N : "0", CultureInfo.InvariantCulture);

        public static int? LerIntOpcional(this Dictionary<string, AttributeValue> item, string nome) =>
            item.TryGetValue(nome, out var v) && v.N is not null ? int.Parse(v.N, CultureInfo.InvariantCulture) : null;

        public static decimal LerDecimal(this Dictionary<string, AttributeValue> item, string nome) =>
            decimal.Parse(item.TryGetValue(nome, out var v) && v.N is not null ? v.N : "0", CultureInfo.InvariantCulture);

        public static bool LerBool(this Dictionary<string, AttributeValue> item, string nome) =>
            item.TryGetValue(nome, out var v) && v.BOOL == true;

        public static DateTime LerData(this Dictionary<string, AttributeValue> item, string nome) =>
            DateTime.Parse(item.LerS(nome), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        public static DateTime? LerDataOpcional(this Dictionary<string, AttributeValue> item, string nome) =>
            item.LerSOpcional(nome) is string s ? DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null;
    }
}
