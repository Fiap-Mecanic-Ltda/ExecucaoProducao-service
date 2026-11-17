using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using ExecucaoService.Infrastructure.Repositories;

namespace ExecucaoService.Infrastructure.Persistencia
{
    /// <summary>
    /// Esquema das tabelas (ADR-003), o mesmo que o Terraform do ExecucaoProducao-InfraSGBD cria
    /// na AWS. Usado para subir o DynamoDB Local e nos testes de integração.
    /// </summary>
    public static class DefinicaoTabelas
    {
        public static IEnumerable<CreateTableRequest> Todas(DynamoDbOptions opcoes)
        {
            yield return new CreateTableRequest
            {
                TableName            = opcoes.Tabela(Tabelas.Ordens),
                BillingMode          = BillingMode.PAY_PER_REQUEST,
                KeySchema            = [Chave(Atributos.Pk, KeyType.HASH), Chave(Atributos.Sk, KeyType.RANGE)],
                AttributeDefinitions =
                [
                    Atributo(Atributos.Pk, ScalarAttributeType.S),
                    Atributo(Atributos.Sk, ScalarAttributeType.S),
                    Atributo("status", ScalarAttributeType.S),
                    Atributo("dataAlteracaoStatus", ScalarAttributeType.S)
                ],
                GlobalSecondaryIndexes =
                [
                    new GlobalSecondaryIndex
                    {
                        IndexName  = ExecucaoRepository.IndiceStatus,
                        KeySchema  = [Chave("status", KeyType.HASH), Chave("dataAlteracaoStatus", KeyType.RANGE)],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL }
                    }
                ]
            };

            yield return new CreateTableRequest
            {
                TableName            = opcoes.Tabela(Tabelas.Estoque),
                BillingMode          = BillingMode.PAY_PER_REQUEST,
                KeySchema            = [Chave(Atributos.Pk, KeyType.HASH)],
                AttributeDefinitions = [Atributo(Atributos.Pk, ScalarAttributeType.S), Atributo("tipo", ScalarAttributeType.N)],
                GlobalSecondaryIndexes =
                [
                    new GlobalSecondaryIndex
                    {
                        IndexName  = EstoqueRepository.IndiceTipo,
                        KeySchema  = [Chave("tipo", KeyType.HASH)],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL }
                    }
                ]
            };

            yield return new CreateTableRequest
            {
                TableName            = opcoes.Tabela(Tabelas.Servicos),
                BillingMode          = BillingMode.PAY_PER_REQUEST,
                KeySchema            = [Chave(Atributos.Pk, KeyType.HASH)],
                AttributeDefinitions = [Atributo(Atributos.Pk, ScalarAttributeType.S)]
            };

            yield return new CreateTableRequest
            {
                TableName            = opcoes.Tabela(Tabelas.Mensagens),
                BillingMode          = BillingMode.PAY_PER_REQUEST,
                KeySchema            = [Chave(Atributos.Pk, KeyType.HASH)],
                AttributeDefinitions = [Atributo(Atributos.Pk, ScalarAttributeType.S)]
            };
        }

        /// <summary>Cria as tabelas que ainda não existem e espera ficarem ativas.</summary>
        public static async Task CriarQueFaltamAsync(IAmazonDynamoDB dynamo, DynamoDbOptions opcoes, CancellationToken ct = default)
        {
            var existentes = new HashSet<string>();
            string? inicio = null;
            do
            {
                var pagina = await dynamo.ListTablesAsync(new ListTablesRequest { ExclusiveStartTableName = inicio }, ct);
                existentes.UnionWith(pagina.TableNames ?? []);
                inicio = pagina.LastEvaluatedTableName;
            }
            while (!string.IsNullOrEmpty(inicio));

            foreach (var tabela in Todas(opcoes).Where(t => !existentes.Contains(t.TableName)))
            {
                await dynamo.CreateTableAsync(tabela, ct);

                for (var tentativa = 0; tentativa < 60; tentativa++)
                {
                    var descricao = await dynamo.DescribeTableAsync(tabela.TableName, ct);
                    if (descricao.Table.TableStatus == TableStatus.ACTIVE)
                        break;
                    await Task.Delay(500, ct);
                }
            }
        }

        private static KeySchemaElement Chave(string nome, KeyType tipo) => new(nome, tipo);

        private static AttributeDefinition Atributo(string nome, ScalarAttributeType tipo) => new(nome, tipo);
    }
}
