using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Globalization;

namespace ExecucaoService.Infrastructure.Persistencia
{
    /// <summary>
    /// Ids inteiros (o contrato usa int para estoqueId e servicoOficinaId) a partir de um contador
    /// atômico guardado na própria tabela, no item de chave CONTADOR.
    /// </summary>
    internal static class Contador
    {
        public const string Chave = "CONTADOR";

        public static async Task<int> ProximoAsync(IAmazonDynamoDB dynamo, string tabela, CancellationToken ct)
        {
            var resposta = await dynamo.UpdateItemAsync(new UpdateItemRequest
            {
                TableName                 = tabela,
                Key                       = new() { [Atributos.Pk] = Atributos.S(Chave) },
                UpdateExpression          = "ADD valor :um",
                ExpressionAttributeValues = new() { [":um"] = Atributos.N(1) },
                ReturnValues              = ReturnValue.UPDATED_NEW
            }, ct);

            return int.Parse(resposta.Attributes["valor"].N, CultureInfo.InvariantCulture);
        }

        /// <summary>Leva o contador até <paramref name="minimo"/>, se ele estiver abaixo. Nunca diminui.</summary>
        public static async Task GarantirMinimoAsync(IAmazonDynamoDB dynamo, string tabela, int minimo, CancellationToken ct)
        {
            try
            {
                await dynamo.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName                 = tabela,
                    Key                       = new() { [Atributos.Pk] = Atributos.S(Chave) },
                    UpdateExpression          = "SET valor = :minimo",
                    ConditionExpression       = "attribute_not_exists(valor) OR valor < :minimo",
                    ExpressionAttributeValues = new() { [":minimo"] = Atributos.N(minimo) }
                }, ct);
            }
            catch (ConditionalCheckFailedException)
            {
                // O contador já está no mínimo ou acima: nada a fazer.
            }
        }
    }
}
