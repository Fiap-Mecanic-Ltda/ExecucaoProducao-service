namespace ExecucaoService.Infrastructure.Persistencia
{
    /// <summary>
    /// Configuração do DynamoDB (seção "DynamoDb"). Na AWS basta a região: as credenciais vêm
    /// da role da instância do k3s. Localmente, <see cref="ServiceUrl"/> aponta para o
    /// DynamoDB Local do docker-compose.
    /// </summary>
    public class DynamoDbOptions
    {
        public const string Secao = "DynamoDb";

        /// <summary>Região AWS das tabelas.</summary>
        public string Regiao { get; set; } = "us-east-1";

        /// <summary>Endpoint alternativo (DynamoDB Local). Vazio na AWS.</summary>
        public string? ServiceUrl { get; set; }

        /// <summary>Prefixo dos nomes das tabelas (ADR-003): execucao-ordens, execucao-estoque…</summary>
        public string PrefixoTabelas { get; set; } = "execucao";

        /// <summary>
        /// Cria as tabelas que faltarem ao subir a API. Só para o DynamoDB Local; na AWS as
        /// tabelas vêm do Terraform do ExecucaoProducao-InfraSGBD.
        /// </summary>
        public bool CriarTabelas { get; set; }

        public string Tabela(string nome) => $"{PrefixoTabelas}-{nome}";
    }
}
