using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Domain.Entities;
using ExecucaoService.Infrastructure.Persistencia;
using Microsoft.Extensions.Options;
using static ExecucaoService.Infrastructure.Persistencia.Atributos;

namespace ExecucaoService.Infrastructure.Repositories
{
    /// <summary>Tabela execucao-servicos: o catálogo é pequeno, então a listagem é um Scan.</summary>
    public class ServicoOficinaRepository : IServicoOficinaRepository
    {
        private const string Prefixo = "SERVICO#";

        private readonly IAmazonDynamoDB _dynamo;
        private readonly string _tabela;

        public ServicoOficinaRepository(IAmazonDynamoDB dynamo, IOptions<DynamoDbOptions> opcoes)
        {
            _dynamo = dynamo;
            _tabela = opcoes.Value.Tabela(Tabelas.Servicos);
        }

        private static Dictionary<string, AttributeValue> Chave(int id) => new() { [Pk] = S(Prefixo + id) };

        public Task<int> ProximoIdAsync(CancellationToken ct = default) => Contador.ProximoAsync(_dynamo, _tabela, ct);

        public async Task<ServicoOficina?> ObterAsync(int id, CancellationToken ct = default)
        {
            var resposta = await _dynamo.GetItemAsync(new GetItemRequest { TableName = _tabela, Key = Chave(id), ConsistentRead = true }, ct);
            return resposta.IsItemSet && resposta.Item.Count > 0 ? Ler(resposta.Item) : null;
        }

        public async Task<IReadOnlyList<ServicoOficina>> ListarAsync(bool somenteAtivos = false, CancellationToken ct = default)
        {
            var servicos = new List<ServicoOficina>();
            Dictionary<string, AttributeValue>? inicio = null;

            do
            {
                var pagina = await _dynamo.ScanAsync(new ScanRequest
                {
                    TableName                 = _tabela,
                    FilterExpression          = "begins_with(pk, :prefixo)",
                    ExpressionAttributeValues = new() { [":prefixo"] = S(Prefixo) },
                    ExclusiveStartKey         = inicio
                }, ct);

                servicos.AddRange(pagina.Items.Select(Ler));
                inicio = pagina.LastEvaluatedKey is { Count: > 0 } ? pagina.LastEvaluatedKey : null;
            }
            while (inicio is not null);

            return somenteAtivos ? servicos.Where(s => s.Ativo).ToList() : servicos;
        }

        public Task SalvarAsync(ServicoOficina servico, CancellationToken ct = default)
        {
            var item = Chave(servico.Id);
            item["id"]           = N(servico.Id);
            item["nome"]         = S(servico.Nome);
            item["valorBase"]    = N(servico.ValorBase);
            item["ativo"]        = Bool(servico.Ativo);
            item["dataCadastro"] = Data(servico.DataCadastro);
            Opcional(item, "descricao", servico.Descricao);
            Opcional(item, "dataAtualizacao", servico.DataAtualizacao);

            return _dynamo.PutItemAsync(new PutItemRequest { TableName = _tabela, Item = item }, ct);
        }

        public Task RemoverAsync(int id, CancellationToken ct = default) =>
            _dynamo.DeleteItemAsync(new DeleteItemRequest { TableName = _tabela, Key = Chave(id) }, ct);

        private static ServicoOficina Ler(Dictionary<string, AttributeValue> item) =>
            ServicoOficina.Restaurar(
                item.LerInt("id"),
                item.LerS("nome"),
                item.LerSOpcional("descricao"),
                item.LerDecimal("valorBase"),
                item.LerBool("ativo"),
                item.LerData("dataCadastro"),
                item.LerDataOpcional("dataAtualizacao"));
    }
}
