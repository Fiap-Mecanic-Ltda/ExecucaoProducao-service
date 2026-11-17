using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Application.Mensageria;
using MechanicLtda.Contratos;
using ExecucaoService.Infrastructure.Persistencia;
using Microsoft.Extensions.Options;
using static ExecucaoService.Infrastructure.Persistencia.Atributos;

namespace ExecucaoService.Infrastructure.Repositories
{
    /// <summary>
    /// Tabela execucao-mensagens: uma linha por mensagem da Saga tratada, com a resposta publicada
    /// (tipo e JSON). O atributo <c>expiraEm</c> (epoch em segundos) é o TTL da tabela no
    /// Terraform: depois de 7 dias uma entrega repetida não é mais esperada (ADR-003).
    /// </summary>
    public class RegistroMensagensRepository : IRegistroMensagens
    {
        public const string AtributoTtl = "expiraEm";
        private static readonly TimeSpan Retencao = TimeSpan.FromDays(7);

        private readonly IAmazonDynamoDB _dynamo;
        private readonly string _tabela;

        public RegistroMensagensRepository(IAmazonDynamoDB dynamo, IOptions<DynamoDbOptions> opcoes)
        {
            _dynamo = dynamo;
            _tabela = opcoes.Value.Tabela(Tabelas.Mensagens);
        }

        public async Task<MensagemTratada?> ObterAsync(string chave, CancellationToken ct = default)
        {
            var resposta = await _dynamo.GetItemAsync(new GetItemRequest
            {
                TableName      = _tabela,
                Key            = new() { [Pk] = S(chave) },
                ConsistentRead = true
            }, ct);

            if (!resposta.IsItemSet || resposta.Item.Count == 0)
                return null;

            var item = resposta.Item;
            return new MensagemTratada(chave, SerializacaoMensagens.Desserializar(item.LerSOpcional("tipoResposta"), item.LerSOpcional("resposta")));
        }

        public Task RegistrarAsync(string chave, MensagemSaga? resposta, DateTime agora, CancellationToken ct = default)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                [Pk]             = S(chave),
                ["processadaEm"] = Data(agora),
                [AtributoTtl]    = new AttributeValue { N = new DateTimeOffset(agora.Add(Retencao)).ToUnixTimeSeconds().ToString() }
            };

            if (resposta is not null)
            {
                var (tipo, json) = SerializacaoMensagens.Serializar(resposta);
                item["tipoResposta"] = S(tipo);
                item["resposta"]     = S(json);
            }

            return _dynamo.PutItemAsync(new PutItemRequest { TableName = _tabela, Item = item }, ct);
        }
    }
}
