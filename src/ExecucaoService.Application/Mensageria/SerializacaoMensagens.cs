using System.Text.Json;
using MechanicLtda.Contratos;

namespace ExecucaoService.Application.Mensageria
{
    /// <summary>Guarda e recupera uma mensagem do contrato como (nome do tipo, JSON).</summary>
    public static class SerializacaoMensagens
    {
        private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

        private static readonly Dictionary<string, Type> Tipos = typeof(MensagemSaga).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(MensagemSaga)) && !t.IsAbstract)
            .ToDictionary(t => t.Name);

        public static (string Tipo, string Json) Serializar(MensagemSaga mensagem) =>
            (mensagem.GetType().Name, JsonSerializer.Serialize(mensagem, mensagem.GetType(), Opcoes));

        public static MensagemSaga? Desserializar(string? tipo, string? json) =>
            tipo is not null && json is not null && Tipos.TryGetValue(tipo, out var t)
                ? (MensagemSaga?)JsonSerializer.Deserialize(json, t, Opcoes)
                : null;
    }
}
