using ExecucaoService.Application.Interfaces;
using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;

namespace ExecucaoService.Testes.Compartilhado
{
    /// <summary>Relógio parado, avançado pelos testes.</summary>
    public sealed class RelogioFixo : IRelogio
    {
        public DateTime UtcAgora { get; set; } = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

        public void Avancar(TimeSpan tempo) => UtcAgora = UtcAgora.Add(tempo);
    }

    /// <summary>
    /// Estoque em memória com as mesmas regras do repositório DynamoDB: reserva e baixa são
    /// tudo-ou-nada e nunca passam do saldo; liberação ignora o que já não está reservado.
    /// </summary>
    public sealed class EstoqueEmMemoria : IEstoqueRepository
    {
        private readonly Dictionary<int, (string Nome, TipoEstoque Tipo, int Atual, int Reservada, int Minima, DateTime Data)> _itens = [];
        private int _contador;

        public Task<int> ProximoIdAsync(CancellationToken ct = default) => Task.FromResult(Interlocked.Increment(ref _contador));

        public Task<Estoque?> ObterAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(_itens.TryGetValue(id, out var e) ? Montar(id, e) : null);

        public Task<IReadOnlyList<Estoque>> ListarAsync(TipoEstoque? tipo = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Estoque>>(_itens.Where(i => tipo is null || i.Value.Tipo == tipo).Select(i => Montar(i.Key, i.Value)).ToList());

        public Task AdicionarAsync(Estoque e, CancellationToken ct = default)
        {
            if (!_itens.TryAdd(e.Id, (e.Nome, e.Tipo, e.QuantidadeAtual, e.QuantidadeReservada, e.QuantidadeMinima, e.DataUltimaAtualizacao)))
                throw new ConflitoDeConcorrenciaException($"Já existe um item de estoque com o id {e.Id}.");
            _contador = Math.Max(_contador, e.Id);
            return Task.CompletedTask;
        }

        public Task AtualizarCadastroAsync(Estoque e, CancellationToken ct = default)
        {
            var atual = Existente(e.Id);
            _itens[e.Id] = atual with { Nome = e.Nome, Tipo = e.Tipo, Minima = e.QuantidadeMinima, Data = e.DataUltimaAtualizacao };
            return Task.CompletedTask;
        }

        public Task RemoverAsync(int id, CancellationToken ct = default)
        {
            if (Existente(id).Reservada > 0)
                throw new RegraDeNegocioException($"O item de estoque {id} não existe mais ou ganhou reservas e não pode ser removido.");
            _itens.Remove(id);
            return Task.CompletedTask;
        }

        public Task<Estoque> ReporAsync(int id, int quantidade, DateTime agora, CancellationToken ct = default)
        {
            var atual = Existente(id);
            _itens[id] = atual with { Atual = atual.Atual + quantidade, Data = agora };
            return Task.FromResult(Montar(id, _itens[id]));
        }

        public Task ReservarAsync(IReadOnlyCollection<ReservaEstoque> reservas, DateTime agora, CancellationToken ct = default)
        {
            foreach (var r in reservas)
                if (!_itens.TryGetValue(r.EstoqueId, out var e) || e.Atual - e.Reservada < r.Quantidade)
                    throw new EstoqueInsuficienteException(r.EstoqueId, $"Estoque insuficiente para reservar {r.Quantidade} unidade(s) do item {r.EstoqueId}.");

            foreach (var r in reservas)
                _itens[r.EstoqueId] = _itens[r.EstoqueId] with { Reservada = _itens[r.EstoqueId].Reservada + r.Quantidade, Data = agora };

            return Task.CompletedTask;
        }

        public Task LiberarAsync(IReadOnlyCollection<ReservaEstoque> reservas, DateTime agora, CancellationToken ct = default)
        {
            foreach (var r in reservas)
                if (_itens.TryGetValue(r.EstoqueId, out var e) && e.Reservada >= r.Quantidade)
                    _itens[r.EstoqueId] = e with { Reservada = e.Reservada - r.Quantidade, Data = agora };

            return Task.CompletedTask;
        }

        public Task BaixarAsync(IReadOnlyCollection<ReservaEstoque> reservas, DateTime agora, CancellationToken ct = default)
        {
            foreach (var r in reservas)
                if (!_itens.TryGetValue(r.EstoqueId, out var e) || e.Reservada < r.Quantidade || e.Atual < r.Quantidade)
                    throw new EstoqueInsuficienteException(r.EstoqueId, $"Estoque insuficiente para baixar {r.Quantidade} unidade(s) do item {r.EstoqueId}.");

            foreach (var r in reservas)
            {
                var e = _itens[r.EstoqueId];
                _itens[r.EstoqueId] = e with { Atual = e.Atual - r.Quantidade, Reservada = e.Reservada - r.Quantidade, Data = agora };
            }

            return Task.CompletedTask;
        }

        private (string Nome, TipoEstoque Tipo, int Atual, int Reservada, int Minima, DateTime Data) Existente(int id) =>
            _itens.TryGetValue(id, out var e) ? e : throw new RecursoNaoEncontradoException($"Item de estoque {id} não encontrado.");

        private static Estoque Montar(int id, (string Nome, TipoEstoque Tipo, int Atual, int Reservada, int Minima, DateTime Data) e) =>
            Estoque.Restaurar(id, e.Nome, e.Tipo, e.Atual, e.Reservada, e.Minima, e.Data);
    }

    public sealed class ServicosEmMemoria : IServicoOficinaRepository
    {
        private readonly Dictionary<int, ServicoOficina> _servicos = [];
        private int _contador;

        public Task<int> ProximoIdAsync(CancellationToken ct = default) => Task.FromResult(Interlocked.Increment(ref _contador));

        public Task<ServicoOficina?> ObterAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(_servicos.GetValueOrDefault(id));

        public Task<IReadOnlyList<ServicoOficina>> ListarAsync(bool somenteAtivos = false, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ServicoOficina>>(_servicos.Values.Where(s => !somenteAtivos || s.Ativo).ToList());

        public Task SalvarAsync(ServicoOficina servico, CancellationToken ct = default)
        {
            _servicos[servico.Id] = servico;
            _contador = Math.Max(_contador, servico.Id);
            return Task.CompletedTask;
        }

        public Task RemoverAsync(int id, CancellationToken ct = default)
        {
            _servicos.Remove(id);
            return Task.CompletedTask;
        }
    }

    /// <summary>Guarda cópias, como um banco: só o que passou por SalvarAsync fica gravado.</summary>
    public sealed class ExecucoesEmMemoria : IExecucaoRepository
    {
        private readonly Dictionary<int, ExecucaoOrdemServico> _gravadas = [];

        /// <summary>Quando preenchida, a próxima gravação falha com esta exceção.</summary>
        public Exception? FalharProximaGravacao { get; set; }

        public Task<ExecucaoOrdemServico?> ObterAsync(int ordemServicoId, CancellationToken ct = default) =>
            Task.FromResult(_gravadas.TryGetValue(ordemServicoId, out var e) ? Copiar(e) : null);

        public Task<IReadOnlyList<ExecucaoOrdemServico>> ListarPorStatusAsync(StatusExecucao status, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExecucaoOrdemServico>>(
                _gravadas.Values.Where(e => e.Status == status).OrderBy(e => e.DataAlteracaoStatus).Select(Copiar).ToList());

        public Task SalvarAsync(ExecucaoOrdemServico execucao, CancellationToken ct = default)
        {
            if (FalharProximaGravacao is { } erro)
            {
                FalharProximaGravacao = null;
                throw erro;
            }

            var versaoGravada = _gravadas.TryGetValue(execucao.OrdemServicoId, out var atual) ? atual.Versao : 0;
            if (versaoGravada != execucao.Versao)
                throw new ConflitoDeConcorrenciaException($"A OS {execucao.OrdemServicoId} foi alterada por outra operação.");

            execucao.MarcarComoGravado();
            _gravadas[execucao.OrdemServicoId] = Copiar(execucao);
            return Task.CompletedTask;
        }

        private static ExecucaoOrdemServico Copiar(ExecucaoOrdemServico e) =>
            ExecucaoOrdemServico.Restaurar(e.OrdemServicoId, e.CorrelationId, e.ClienteId, e.VeiculoId, e.DescricaoProblema,
                e.Status, e.Observacao, e.MotivoCancelamento, e.DataEntrada, e.DataAlteracaoStatus, e.DataConclusaoDiagnostico,
                e.DataInicio, e.DataFim, e.Versao, e.Itens, e.Reservas, e.Historico.Concat(e.HistoricoNaoGravado));
    }
}
