using System.Collections.Concurrent;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Domain.Entities;
using MechanicLtda.Contratos;

namespace ExecucaoService.Testes.Compartilhado
{
    /// <summary>Guarda os eventos publicados para os testes conferirem.</summary>
    public sealed class PublicadorEmMemoria : IPublicadorEventos
    {
        private readonly ConcurrentQueue<MensagemSaga> _publicados = new();

        public IReadOnlyList<MensagemSaga> Publicados => _publicados.ToList();

        public IReadOnlyList<T> DoTipo<T>() where T : MensagemSaga => _publicados.OfType<T>().ToList();

        public Task PublicarAsync<T>(T evento, CancellationToken ct = default) where T : MensagemSaga
        {
            _publicados.Enqueue(evento);
            return Task.CompletedTask;
        }
    }

    public sealed class RegistroMensagensEmMemoria : IRegistroMensagens
    {
        private readonly ConcurrentDictionary<string, MensagemTratada> _chaves = new();

        public int Quantidade => _chaves.Count;

        public Task<MensagemTratada?> ObterAsync(string chave, CancellationToken ct = default) =>
            Task.FromResult(_chaves.GetValueOrDefault(chave));

        public Task RegistrarAsync(string chave, MensagemSaga? resposta, DateTime agora, CancellationToken ct = default)
        {
            _chaves[chave] = new MensagemTratada(chave, resposta);
            return Task.CompletedTask;
        }
    }

    public sealed class MonitoramentoEmMemoria : IMonitoramentoService
    {
        private readonly ConcurrentQueue<string> _passos = new();
        private readonly ConcurrentQueue<string> _falhas = new();

        public IReadOnlyList<string> Passos => _passos.ToList();
        public IReadOnlyList<string> Falhas => _falhas.ToList();

        public void RegistrarPasso(ExecucaoOrdemServico execucao, string passo) => _passos.Enqueue($"{execucao.OrdemServicoId}:{passo}");

        public void RegistrarFalha(Guid correlationId, int ordemServicoId, string evento, string motivo) => _falhas.Enqueue($"{ordemServicoId}:{evento}");
    }
}
