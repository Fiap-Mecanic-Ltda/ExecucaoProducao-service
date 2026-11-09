using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;
using ExecucaoService.Infrastructure.Persistencia;
using Microsoft.Extensions.Options;
using System.Globalization;
using static ExecucaoService.Infrastructure.Persistencia.Atributos;

namespace ExecucaoService.Infrastructure.Repositories
{
    /// <summary>
    /// Tabela execucao-ordens. Cada OS ocupa uma partição (pk = OS#id): o estado atual no item
    /// sk = EXECUCAO, com itens e reservas como listas de mapas, e uma linha por mudança de status
    /// (sk = HIST#data#seq). Só o item EXECUCAO tem o atributo status, então o índice de status
    /// (a fila) só enxerga ele.
    /// </summary>
    public class ExecucaoRepository : IExecucaoRepository
    {
        private const string Prefixo = "OS#";
        private const string SkExecucao = "EXECUCAO";
        private const string PrefixoHistorico = "HIST#";
        public const string IndiceStatus = "status-index";

        private readonly IAmazonDynamoDB _dynamo;
        private readonly string _tabela;

        public ExecucaoRepository(IAmazonDynamoDB dynamo, IOptions<DynamoDbOptions> opcoes)
        {
            _dynamo = dynamo;
            _tabela = opcoes.Value.Tabela(Tabelas.Ordens);
        }

        public async Task<ExecucaoOrdemServico?> ObterAsync(int ordemServicoId, CancellationToken ct = default)
        {
            var itens = new List<Dictionary<string, AttributeValue>>();
            Dictionary<string, AttributeValue>? inicio = null;

            do
            {
                var pagina = await _dynamo.QueryAsync(new QueryRequest
                {
                    TableName                 = _tabela,
                    KeyConditionExpression    = "pk = :pk",
                    ExpressionAttributeValues = new() { [":pk"] = S(Prefixo + ordemServicoId) },
                    ConsistentRead            = true,
                    ExclusiveStartKey         = inicio
                }, ct);

                itens.AddRange(pagina.Items);
                inicio = pagina.LastEvaluatedKey is { Count: > 0 } ? pagina.LastEvaluatedKey : null;
            }
            while (inicio is not null);

            var principal = itens.FirstOrDefault(i => i.LerS(Sk) == SkExecucao);
            if (principal is null)
                return null;

            var historico = itens.Where(i => i.LerS(Sk).StartsWith(PrefixoHistorico, StringComparison.Ordinal)).Select(LerHistorico);
            return Ler(principal, historico);
        }

        public async Task<IReadOnlyList<ExecucaoOrdemServico>> ListarPorStatusAsync(StatusExecucao status, CancellationToken ct = default)
        {
            var execucoes = new List<ExecucaoOrdemServico>();
            Dictionary<string, AttributeValue>? inicio = null;

            do
            {
                var pagina = await _dynamo.QueryAsync(new QueryRequest
                {
                    TableName                 = _tabela,
                    IndexName                 = IndiceStatus,
                    KeyConditionExpression    = "#status = :status",
                    ExpressionAttributeNames  = new() { ["#status"] = "status" },
                    ExpressionAttributeValues = new() { [":status"] = S(status.ToString()) },
                    ScanIndexForward          = true,
                    ExclusiveStartKey         = inicio
                }, ct);

                // A fila não mostra o histórico; ele só é lido no detalhe da OS.
                execucoes.AddRange(pagina.Items.Select(i => Ler(i, [])));
                inicio = pagina.LastEvaluatedKey is { Count: > 0 } ? pagina.LastEvaluatedKey : null;
            }
            while (inicio is not null);

            return execucoes;
        }

        public async Task SalvarAsync(ExecucaoOrdemServico execucao, CancellationToken ct = default)
        {
            var put = new Put { TableName = _tabela, Item = Escrever(execucao) };

            if (execucao.Versao == 0)
            {
                put.ConditionExpression = "attribute_not_exists(pk)";
            }
            else
            {
                put.ConditionExpression       = "versao = :versaoLida";
                put.ExpressionAttributeValues = new() { [":versaoLida"] = N(execucao.Versao) };
            }

            var transacao = new List<TransactWriteItem> { new() { Put = put } };
            var sequencia = 0;
            foreach (var h in execucao.HistoricoNaoGravado)
                transacao.Add(new TransactWriteItem { Put = new Put { TableName = _tabela, Item = EscreverHistorico(execucao.OrdemServicoId, h, sequencia++) } });

            try
            {
                await _dynamo.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = transacao }, ct);
            }
            catch (TransactionCanceledException ex) when (ex.CancellationReasons?.FirstOrDefault()?.Code == "ConditionalCheckFailed")
            {
                throw new ConflitoDeConcorrenciaException(
                    $"A OS {execucao.OrdemServicoId} foi alterada por outra operação. Consulte de novo e repita.");
            }

            execucao.MarcarComoGravado();
        }

        private static Dictionary<string, AttributeValue> Escrever(ExecucaoOrdemServico e)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                [Pk]                    = S(Prefixo + e.OrdemServicoId),
                [Sk]                    = S(SkExecucao),
                ["ordemServicoId"]      = N(e.OrdemServicoId),
                ["correlationId"]       = S(e.CorrelationId.ToString()),
                ["status"]              = S(e.Status.ToString()),
                ["dataEntrada"]         = Data(e.DataEntrada),
                ["dataAlteracaoStatus"] = Data(e.DataAlteracaoStatus),
                ["versao"]              = N(e.Versao + 1),
                ["itens"]               = new AttributeValue { L = e.Itens.Select(EscreverItem).ToList(), IsLSet = true },
                ["reservas"]            = new AttributeValue { L = e.Reservas.Select(EscreverReserva).ToList(), IsLSet = true }
            };

            Opcional(item, "clienteId", e.ClienteId);
            Opcional(item, "veiculoId", e.VeiculoId);
            Opcional(item, "descricaoProblema", e.DescricaoProblema);
            Opcional(item, "observacao", e.Observacao);
            Opcional(item, "motivoCancelamento", e.MotivoCancelamento);
            Opcional(item, "dataConclusaoDiagnostico", e.DataConclusaoDiagnostico);
            Opcional(item, "dataInicio", e.DataInicio);
            Opcional(item, "dataFim", e.DataFim);

            return item;
        }

        private static AttributeValue EscreverItem(ItemExecucao i)
        {
            var mapa = new Dictionary<string, AttributeValue>
            {
                ["descricao"]     = S(i.Descricao),
                ["tipo"]          = S(i.Tipo.ToString()),
                ["quantidade"]    = N(i.Quantidade),
                ["valorUnitario"] = N(i.ValorUnitario)
            };
            Opcional(mapa, "estoqueId", i.EstoqueId);
            Opcional(mapa, "servicoOficinaId", i.ServicoOficinaId);
            return new AttributeValue { M = mapa, IsMSet = true };
        }

        private static AttributeValue EscreverReserva(ReservaEstoque r) =>
            new() { M = new() { ["estoqueId"] = N(r.EstoqueId), ["quantidade"] = N(r.Quantidade) }, IsMSet = true };

        private static Dictionary<string, AttributeValue> EscreverHistorico(int ordemServicoId, HistoricoExecucao h, int sequencia)
        {
            var dataHora = DateTime.SpecifyKind(h.DataHora, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
            var item = new Dictionary<string, AttributeValue>
            {
                [Pk]           = S(Prefixo + ordemServicoId),
                // A sequência desempata transições gravadas no mesmo instante (ex.: entrada + início do diagnóstico).
                [Sk]           = S($"{PrefixoHistorico}{dataHora}#{sequencia:D3}#{Guid.NewGuid():N}"),
                ["statusNovo"] = S(h.StatusNovo.ToString()),
                ["dataHora"]   = Data(h.DataHora)
            };
            Opcional(item, "statusAnterior", h.StatusAnterior?.ToString());
            Opcional(item, "observacao", h.Observacao);
            return item;
        }

        private static HistoricoExecucao LerHistorico(Dictionary<string, AttributeValue> item) =>
            new(item.LerSOpcional("statusAnterior") is string anterior ? Enum.Parse<StatusExecucao>(anterior) : null,
                Enum.Parse<StatusExecucao>(item.LerS("statusNovo")),
                item.LerData("dataHora"),
                item.LerSOpcional("observacao"));

        private static ExecucaoOrdemServico Ler(Dictionary<string, AttributeValue> item, IEnumerable<HistoricoExecucao> historico)
        {
            var itens = item.TryGetValue("itens", out var l) && l.L is not null
                ? l.L.Select(v => new ItemExecucao(
                    v.M.LerS("descricao"),
                    Enum.Parse<TipoItem>(v.M.LerS("tipo")),
                    v.M.LerInt("quantidade"),
                    v.M.LerDecimal("valorUnitario"),
                    v.M.LerIntOpcional("estoqueId"),
                    v.M.LerIntOpcional("servicoOficinaId")))
                : [];

            var reservas = item.TryGetValue("reservas", out var r) && r.L is not null
                ? r.L.Select(v => new ReservaEstoque(v.M.LerInt("estoqueId"), v.M.LerInt("quantidade")))
                : [];

            return ExecucaoOrdemServico.Restaurar(
                item.LerInt("ordemServicoId"),
                Guid.Parse(item.LerS("correlationId")),
                item.LerIntOpcional("clienteId"),
                item.LerIntOpcional("veiculoId"),
                item.LerSOpcional("descricaoProblema"),
                Enum.Parse<StatusExecucao>(item.LerS("status")),
                item.LerSOpcional("observacao"),
                item.LerSOpcional("motivoCancelamento"),
                item.LerData("dataEntrada"),
                item.LerData("dataAlteracaoStatus"),
                item.LerDataOpcional("dataConclusaoDiagnostico"),
                item.LerDataOpcional("dataInicio"),
                item.LerDataOpcional("dataFim"),
                item.LerInt("versao"),
                itens, reservas, historico);
        }
    }
}
