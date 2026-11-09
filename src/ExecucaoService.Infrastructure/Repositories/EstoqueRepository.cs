using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;
using ExecucaoService.Infrastructure.Persistencia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static ExecucaoService.Infrastructure.Persistencia.Atributos;

namespace ExecucaoService.Infrastructure.Repositories
{
    /// <summary>
    /// Tabela execucao-estoque. Além da quantidade atual e da reservada, cada item guarda a
    /// quantidade disponível: as expressões de condição do DynamoDB não fazem conta, então é ela
    /// que garante, na própria escrita, que uma reserva nunca passa do saldo.
    /// </summary>
    public class EstoqueRepository : IEstoqueRepository
    {
        private const string Prefixo = "ESTOQUE#";
        public const string IndiceTipo = "tipo-index";

        private readonly IAmazonDynamoDB _dynamo;
        private readonly ILogger<EstoqueRepository> _logger;
        private readonly string _tabela;

        public EstoqueRepository(IAmazonDynamoDB dynamo, IOptions<DynamoDbOptions> opcoes, ILogger<EstoqueRepository> logger)
        {
            _dynamo = dynamo;
            _logger = logger;
            _tabela = opcoes.Value.Tabela(Tabelas.Estoque);
        }

        private static Dictionary<string, AttributeValue> Chave(int id) => new() { [Pk] = S(Prefixo + id) };

        public Task<int> ProximoIdAsync(CancellationToken ct = default) => Contador.ProximoAsync(_dynamo, _tabela, ct);

        public Task GarantirIdMinimoAsync(int ultimoId, CancellationToken ct = default) => Contador.GarantirMinimoAsync(_dynamo, _tabela, ultimoId, ct);

        public async Task<Estoque?> ObterAsync(int id, CancellationToken ct = default)
        {
            var resposta = await _dynamo.GetItemAsync(new GetItemRequest { TableName = _tabela, Key = Chave(id), ConsistentRead = true }, ct);
            return resposta.IsItemSet && resposta.Item.Count > 0 ? Ler(resposta.Item) : null;
        }

        public async Task<IReadOnlyList<Estoque>> ListarAsync(TipoEstoque? tipo = null, CancellationToken ct = default)
        {
            var itens = new List<Estoque>();
            Dictionary<string, AttributeValue>? inicio = null;

            do
            {
                if (tipo is null)
                {
                    var pagina = await _dynamo.ScanAsync(new ScanRequest
                    {
                        TableName                 = _tabela,
                        FilterExpression          = "begins_with(pk, :prefixo)",
                        ExpressionAttributeValues = new() { [":prefixo"] = S(Prefixo) },
                        ExclusiveStartKey         = inicio
                    }, ct);
                    itens.AddRange(pagina.Items.Select(Ler));
                    inicio = pagina.LastEvaluatedKey is { Count: > 0 } ? pagina.LastEvaluatedKey : null;
                }
                else
                {
                    var pagina = await _dynamo.QueryAsync(new QueryRequest
                    {
                        TableName                 = _tabela,
                        IndexName                 = IndiceTipo,
                        KeyConditionExpression    = "tipo = :tipo",
                        ExpressionAttributeValues = new() { [":tipo"] = N((int)tipo.Value) },
                        ExclusiveStartKey         = inicio
                    }, ct);
                    itens.AddRange(pagina.Items.Select(Ler));
                    inicio = pagina.LastEvaluatedKey is { Count: > 0 } ? pagina.LastEvaluatedKey : null;
                }
            }
            while (inicio is not null);

            return itens;
        }

        public async Task AdicionarAsync(Estoque estoque, CancellationToken ct = default)
        {
            var item = Chave(estoque.Id);
            item["id"]                    = N(estoque.Id);
            item["nome"]                  = S(estoque.Nome);
            item["tipo"]                  = N((int)estoque.Tipo);
            item["quantidadeAtual"]       = N(estoque.QuantidadeAtual);
            item["quantidadeReservada"]   = N(estoque.QuantidadeReservada);
            item["quantidadeDisponivel"]  = N(estoque.QuantidadeDisponivel);
            item["quantidadeMinima"]      = N(estoque.QuantidadeMinima);
            item["dataUltimaAtualizacao"] = Data(estoque.DataUltimaAtualizacao);

            try
            {
                await _dynamo.PutItemAsync(new PutItemRequest
                {
                    TableName           = _tabela,
                    Item                = item,
                    ConditionExpression = "attribute_not_exists(pk)"
                }, ct);
            }
            catch (ConditionalCheckFailedException)
            {
                throw new ConflitoDeConcorrenciaException($"Já existe um item de estoque com o id {estoque.Id}.");
            }
        }

        public async Task AtualizarCadastroAsync(Estoque estoque, CancellationToken ct = default)
        {
            try
            {
                await _dynamo.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName                = _tabela,
                    Key                      = Chave(estoque.Id),
                    UpdateExpression         = "SET nome = :nome, tipo = :tipo, quantidadeMinima = :minima, dataUltimaAtualizacao = :agora",
                    ConditionExpression      = "attribute_exists(pk)",
                    ExpressionAttributeValues = new()
                    {
                        [":nome"]   = S(estoque.Nome),
                        [":tipo"]   = N((int)estoque.Tipo),
                        [":minima"] = N(estoque.QuantidadeMinima),
                        [":agora"]  = Data(estoque.DataUltimaAtualizacao)
                    }
                }, ct);
            }
            catch (ConditionalCheckFailedException)
            {
                throw new RecursoNaoEncontradoException($"Item de estoque {estoque.Id} não encontrado.");
            }
        }

        public async Task RemoverAsync(int id, CancellationToken ct = default)
        {
            try
            {
                await _dynamo.DeleteItemAsync(new DeleteItemRequest
                {
                    TableName                 = _tabela,
                    Key                       = Chave(id),
                    ConditionExpression       = "attribute_exists(pk) AND quantidadeReservada = :zero",
                    ExpressionAttributeValues = new() { [":zero"] = N(0) }
                }, ct);
            }
            catch (ConditionalCheckFailedException)
            {
                throw new RegraDeNegocioException($"O item de estoque {id} não existe mais ou ganhou reservas e não pode ser removido.");
            }
        }

        public async Task<Estoque> ReporAsync(int id, int quantidade, DateTime agora, CancellationToken ct = default)
        {
            try
            {
                var resposta = await _dynamo.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName                 = _tabela,
                    Key                       = Chave(id),
                    UpdateExpression          = "SET quantidadeAtual = quantidadeAtual + :q, quantidadeDisponivel = quantidadeDisponivel + :q, dataUltimaAtualizacao = :agora",
                    ConditionExpression       = "attribute_exists(pk)",
                    ExpressionAttributeValues = new() { [":q"] = N(quantidade), [":agora"] = Data(agora) },
                    ReturnValues              = ReturnValue.ALL_NEW
                }, ct);

                return Ler(resposta.Attributes);
            }
            catch (ConditionalCheckFailedException)
            {
                throw new RecursoNaoEncontradoException($"Item de estoque {id} não encontrado.");
            }
        }

        public async Task ReservarAsync(IReadOnlyCollection<ReservaEstoque> reservas, DateTime agora, CancellationToken ct = default)
        {
            if (reservas.Count == 0)
                return;

            var lista = reservas.ToList();
            var transacao = lista.Select(r => new TransactWriteItem
            {
                Update = new Update
                {
                    TableName                 = _tabela,
                    Key                       = Chave(r.EstoqueId),
                    UpdateExpression          = "SET quantidadeReservada = quantidadeReservada + :q, quantidadeDisponivel = quantidadeDisponivel - :q, dataUltimaAtualizacao = :agora",
                    ConditionExpression       = "attribute_exists(pk) AND quantidadeDisponivel >= :q",
                    ExpressionAttributeValues = new() { [":q"] = N(r.Quantidade), [":agora"] = Data(agora) }
                }
            }).ToList();

            await ExecutarTransacaoDeSaldoAsync(transacao, lista, "reservar", ct);
        }

        public async Task LiberarAsync(IReadOnlyCollection<ReservaEstoque> reservas, DateTime agora, CancellationToken ct = default)
        {
            // Item a item, sem transação: a compensação devolve o que puder. Uma condição que
            // falha quer dizer que a reserva já não está lá (já liberada ou baixada).
            foreach (var r in reservas)
            {
                try
                {
                    await _dynamo.UpdateItemAsync(new UpdateItemRequest
                    {
                        TableName                 = _tabela,
                        Key                       = Chave(r.EstoqueId),
                        UpdateExpression          = "SET quantidadeReservada = quantidadeReservada - :q, quantidadeDisponivel = quantidadeDisponivel + :q, dataUltimaAtualizacao = :agora",
                        ConditionExpression       = "attribute_exists(pk) AND quantidadeReservada >= :q",
                        ExpressionAttributeValues = new() { [":q"] = N(r.Quantidade), [":agora"] = Data(agora) }
                    }, ct);
                }
                catch (ConditionalCheckFailedException)
                {
                    _logger.LogWarning("Reserva de {Quantidade} do estoque {EstoqueId} não encontrada ao liberar; ignorada.",
                        r.Quantidade, r.EstoqueId);
                }
            }
        }

        public async Task BaixarAsync(IReadOnlyCollection<ReservaEstoque> reservas, DateTime agora, CancellationToken ct = default)
        {
            if (reservas.Count == 0)
                return;

            var lista = reservas.ToList();
            var transacao = lista.Select(r => new TransactWriteItem
            {
                Update = new Update
                {
                    TableName                 = _tabela,
                    Key                       = Chave(r.EstoqueId),
                    UpdateExpression          = "SET quantidadeAtual = quantidadeAtual - :q, quantidadeReservada = quantidadeReservada - :q, dataUltimaAtualizacao = :agora",
                    ConditionExpression       = "attribute_exists(pk) AND quantidadeReservada >= :q AND quantidadeAtual >= :q",
                    ExpressionAttributeValues = new() { [":q"] = N(r.Quantidade), [":agora"] = Data(agora) }
                }
            }).ToList();

            await ExecutarTransacaoDeSaldoAsync(transacao, lista, "baixar", ct);
        }

        private async Task ExecutarTransacaoDeSaldoAsync(List<TransactWriteItem> transacao, List<ReservaEstoque> reservas,
                                                         string operacao, CancellationToken ct)
        {
            try
            {
                await _dynamo.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = transacao }, ct);
            }
            catch (TransactionCanceledException ex)
            {
                // CancellationReasons vem na mesma ordem dos itens da transação.
                var motivos = ex.CancellationReasons ?? [];
                for (var i = 0; i < motivos.Count && i < reservas.Count; i++)
                {
                    if (motivos[i].Code == "ConditionalCheckFailed")
                    {
                        var r = reservas[i];
                        throw new EstoqueInsuficienteException(r.EstoqueId,
                            $"Estoque insuficiente para {operacao} {r.Quantidade} unidade(s) do item {r.EstoqueId}.");
                    }
                }

                throw;
            }
        }

        private static Estoque Ler(Dictionary<string, AttributeValue> item) =>
            Estoque.Restaurar(
                item.LerInt("id"),
                item.LerS("nome"),
                (TipoEstoque)item.LerInt("tipo"),
                item.LerInt("quantidadeAtual"),
                item.LerInt("quantidadeReservada"),
                item.LerInt("quantidadeMinima"),
                item.LerData("dataUltimaAtualizacao"));
    }
}
