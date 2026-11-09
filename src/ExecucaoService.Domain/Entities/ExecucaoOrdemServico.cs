using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;

namespace ExecucaoService.Domain.Entities
{
    /// <summary>
    /// Uma OS na Execução: entra pela Saga (IniciarDiagnostico), passa pelo diagnóstico e pelo
    /// reparo e sai Finalizada ou Cancelada. Toda mudança de status passa por
    /// <see cref="MudarStatus"/>, que valida a transição e registra o histórico.
    /// </summary>
    public class ExecucaoOrdemServico
    {
        public const int TamanhoMaximoTexto = 1000;

        private readonly List<ItemExecucao> _itens = [];
        private readonly List<ReservaEstoque> _reservas = [];
        private readonly List<HistoricoExecucao> _historico = [];
        private readonly List<HistoricoExecucao> _historicoNovo = [];

        public int OrdemServicoId { get; private set; }

        /// <summary>Id da Saga no OS Service; vai em todos os eventos publicados.</summary>
        public Guid CorrelationId { get; private set; }
        public int? ClienteId { get; private set; }
        public int? VeiculoId { get; private set; }
        public string? DescricaoProblema { get; private set; }
        public StatusExecucao Status { get; private set; }
        public string? Observacao { get; private set; }
        public string? MotivoCancelamento { get; private set; }

        public DateTime DataEntrada { get; private set; }
        public DateTime DataAlteracaoStatus { get; private set; }
        public DateTime? DataConclusaoDiagnostico { get; private set; }
        public DateTime? DataInicio { get; private set; }
        public DateTime? DataFim { get; private set; }

        /// <summary>Versão para gravação otimista: o repositório só grava se ninguém gravou antes.</summary>
        public int Versao { get; private set; }

        public IReadOnlyList<ItemExecucao> Itens => _itens.AsReadOnly();

        /// <summary>Estoque reservado no diagnóstico e ainda não baixado nem liberado.</summary>
        public IReadOnlyList<ReservaEstoque> Reservas => _reservas.AsReadOnly();

        public IReadOnlyList<HistoricoExecucao> Historico => _historico.AsReadOnly();

        /// <summary>Linhas de histórico criadas desde a última gravação.</summary>
        public IReadOnlyList<HistoricoExecucao> HistoricoNaoGravado => _historicoNovo.AsReadOnly();

        public decimal ValorTotalItens => _itens.Sum(i => i.ValorTotal);

        private ExecucaoOrdemServico() { }

        /// <summary>Entrada na fila pelo comando IniciarDiagnostico (ou manualmente, sem a Saga).</summary>
        public static ExecucaoOrdemServico Receber(int ordemServicoId, Guid correlationId, int? clienteId, int? veiculoId,
                                                   string? descricaoProblema, DateTime agora)
        {
            if (ordemServicoId <= 0)
                throw new RegraDeNegocioException("OrdemServicoId inválido.");
            if (descricaoProblema is not null && descricaoProblema.Length > TamanhoMaximoTexto)
                throw new RegraDeNegocioException($"A descrição do problema tem no máximo {TamanhoMaximoTexto} caracteres.");

            var execucao = new ExecucaoOrdemServico
            {
                OrdemServicoId      = ordemServicoId,
                CorrelationId       = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId,
                ClienteId           = clienteId,
                VeiculoId           = veiculoId,
                DescricaoProblema   = string.IsNullOrWhiteSpace(descricaoProblema) ? null : descricaoProblema.Trim(),
                Status              = StatusExecucao.AguardandoDiagnostico,
                DataEntrada         = agora,
                DataAlteracaoStatus = agora
            };

            execucao.Registrar(null, StatusExecucao.AguardandoDiagnostico, agora, "Entrada na fila de diagnóstico");
            return execucao;
        }

        /// <summary>Recria a OS a partir do que está gravado (repositório).</summary>
        public static ExecucaoOrdemServico Restaurar(
            int ordemServicoId, Guid correlationId, int? clienteId, int? veiculoId, string? descricaoProblema,
            StatusExecucao status, string? observacao, string? motivoCancelamento,
            DateTime dataEntrada, DateTime dataAlteracaoStatus, DateTime? dataConclusaoDiagnostico,
            DateTime? dataInicio, DateTime? dataFim, int versao,
            IEnumerable<ItemExecucao> itens, IEnumerable<ReservaEstoque> reservas, IEnumerable<HistoricoExecucao> historico)
        {
            var execucao = new ExecucaoOrdemServico
            {
                OrdemServicoId           = ordemServicoId,
                CorrelationId            = correlationId,
                ClienteId                = clienteId,
                VeiculoId                = veiculoId,
                DescricaoProblema        = descricaoProblema,
                Status                   = status,
                Observacao               = observacao,
                MotivoCancelamento       = motivoCancelamento,
                DataEntrada              = dataEntrada,
                DataAlteracaoStatus      = dataAlteracaoStatus,
                DataConclusaoDiagnostico = dataConclusaoDiagnostico,
                DataInicio               = dataInicio,
                DataFim                  = dataFim,
                Versao                   = versao
            };

            execucao._itens.AddRange(itens);
            execucao._reservas.AddRange(reservas);
            execucao._historico.AddRange(historico.OrderBy(h => h.DataHora));
            return execucao;
        }

        /// <summary>
        /// Uma nova Saga para a mesma OS (o OS Service recomeça depois de um DiagnosticoFalhou ou
        /// de um prazo vencido). Só vale para uma OS cancelada; o histórico anterior é mantido.
        /// </summary>
        public void RecomecarSaga(Guid correlationId, string? descricaoProblema, DateTime agora)
        {
            if (Status != StatusExecucao.Cancelada)
                throw new RegraDeNegocioException(
                    $"A OS {OrdemServicoId} está em '{Status}' na Execução e não pode recomeçar o diagnóstico.");

            var anterior = Status;
            CorrelationId            = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
            DescricaoProblema        = string.IsNullOrWhiteSpace(descricaoProblema) ? DescricaoProblema : descricaoProblema.Trim();
            Status                   = StatusExecucao.AguardandoDiagnostico;
            DataEntrada              = agora;
            DataAlteracaoStatus      = agora;
            DataConclusaoDiagnostico = null;
            DataInicio               = null;
            DataFim                  = null;
            Observacao               = null;
            MotivoCancelamento       = null;
            _itens.Clear();
            _reservas.Clear();

            Registrar(anterior, StatusExecucao.AguardandoDiagnostico, agora, "Nova entrada na fila de diagnóstico");
        }

        /// <summary>Um mecânico assume o diagnóstico.</summary>
        public void IniciarDiagnostico(DateTime agora) =>
            MudarStatus(StatusExecucao.EmDiagnostico, agora, "Diagnóstico iniciado");

        /// <summary>
        /// Registra os itens do diagnóstico e as reservas de estoque que eles geraram. O repositório
        /// de estoque já reservou as quantidades antes desta chamada.
        /// </summary>
        public void ConcluirDiagnostico(IReadOnlyCollection<ItemExecucao> itens, string? observacao, DateTime agora)
        {
            if (Status == StatusExecucao.AguardandoDiagnostico)
                MudarStatus(StatusExecucao.EmDiagnostico, agora, "Diagnóstico iniciado");

            if (itens.Count == 0)
                throw new RegraDeNegocioException("O diagnóstico precisa de pelo menos um item (peça, insumo ou serviço).");
            if (observacao is not null && observacao.Length > TamanhoMaximoTexto)
                throw new RegraDeNegocioException($"A observação tem no máximo {TamanhoMaximoTexto} caracteres.");

            MudarStatus(StatusExecucao.DiagnosticoConcluido, agora, $"{itens.Count} item(ns) registrado(s)");

            _itens.Clear();
            _itens.AddRange(itens);
            _reservas.Clear();
            _reservas.AddRange(ReservasNecessarias(itens));

            Observacao               = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
            DataConclusaoDiagnostico = agora;
        }

        /// <summary>
        /// O OS Service reenviou IniciarDiagnostico (orçamento falhou): o diagnóstico é refeito. As
        /// reservas anteriores são devolvidas pelo chamador, a partir de <see cref="Reservas"/>.
        /// </summary>
        public void ReabrirDiagnostico(DateTime agora)
        {
            MudarStatus(StatusExecucao.EmDiagnostico, agora, "Diagnóstico reaberto a pedido do OS Service");
            _itens.Clear();
            _reservas.Clear();
            DataConclusaoDiagnostico = null;
        }

        /// <summary>Pagamento confirmado (IniciarExecucao): o estoque reservado foi baixado e a OS aguarda um mecânico.</summary>
        public void EntrarNaFila(DateTime agora)
        {
            MudarStatus(StatusExecucao.NaFila, agora, "Pagamento confirmado; estoque baixado");
            _reservas.Clear();
        }

        public void IniciarReparo(DateTime agora)
        {
            MudarStatus(StatusExecucao.EmExecucao, agora, "Reparo iniciado");
            DataInicio = agora;
        }

        public void Finalizar(DateTime agora, string? observacao = null)
        {
            if (observacao is not null && observacao.Length > TamanhoMaximoTexto)
                throw new RegraDeNegocioException($"A observação tem no máximo {TamanhoMaximoTexto} caracteres.");

            MudarStatus(StatusExecucao.Finalizada, agora, string.IsNullOrWhiteSpace(observacao) ? "Reparo concluído" : observacao.Trim());
            DataFim = agora;
        }

        /// <summary>Compensação: as reservas foram devolvidas pelo chamador.</summary>
        public void Cancelar(string motivo, DateTime agora)
        {
            var texto = string.IsNullOrWhiteSpace(motivo) ? "Cancelada" : motivo.Trim();
            if (texto.Length > TamanhoMaximoTexto)
                texto = texto[..TamanhoMaximoTexto];

            MudarStatus(StatusExecucao.Cancelada, agora, texto);
            MotivoCancelamento = texto;
            _reservas.Clear();
        }

        /// <summary>Chamado pelo repositório depois de gravar: o histórico novo passa a ser histórico gravado.</summary>
        public void MarcarComoGravado()
        {
            _historico.AddRange(_historicoNovo);
            _historicoNovo.Clear();
            Versao++;
        }

        /// <summary>Soma as quantidades de peças e insumos por item de estoque.</summary>
        public static IReadOnlyList<ReservaEstoque> ReservasNecessarias(IEnumerable<ItemExecucao> itens) =>
            itens.Where(i => i.ConsomeEstoque)
                 .GroupBy(i => i.EstoqueId!.Value)
                 .Select(g => new ReservaEstoque(g.Key, g.Sum(i => i.Quantidade)))
                 .OrderBy(r => r.EstoqueId)
                 .ToList();

        private void MudarStatus(StatusExecucao novo, DateTime agora, string? observacao)
        {
            if (!Status.PodeIrPara(novo))
                throw new RegraDeNegocioException($"A OS {OrdemServicoId} não pode passar de '{Status}' para '{novo}'.");

            var anterior = Status;
            Status              = novo;
            DataAlteracaoStatus = agora;
            Registrar(anterior, novo, agora, observacao);
        }

        private void Registrar(StatusExecucao? anterior, StatusExecucao novo, DateTime agora, string? observacao) =>
            _historicoNovo.Add(new HistoricoExecucao(anterior, novo, agora, observacao));
    }
}
