using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;

namespace ExecucaoService.Application.Interfaces
{
    /// <summary>
    /// Estoque no DynamoDB. Reserva, liberação, baixa e reposição são atômicas (escrita
    /// condicional): duas OS nunca reservam o mesmo saldo.
    /// </summary>
    public interface IEstoqueRepository
    {
        Task<int> ProximoIdAsync(CancellationToken ct = default);
        Task<Estoque?> ObterAsync(int id, CancellationToken ct = default);
        Task<IReadOnlyList<Estoque>> ListarAsync(TipoEstoque? tipo = null, CancellationToken ct = default);
        Task AdicionarAsync(Estoque estoque, CancellationToken ct = default);

        /// <summary>Grava nome, tipo e mínimo, sem tocar nas quantidades.</summary>
        Task AtualizarCadastroAsync(Estoque estoque, CancellationToken ct = default);

        /// <summary>Remove o item só se ele não tiver reserva.</summary>
        Task RemoverAsync(int id, CancellationToken ct = default);

        /// <summary>Soma <paramref name="quantidade"/> à quantidade atual e devolve o item atualizado.</summary>
        Task<Estoque> ReporAsync(int id, int quantidade, DateTime agora, CancellationToken ct = default);

        /// <summary>
        /// Reserva todas as quantidades numa única transação: ou tudo, ou nada. Lança
        /// <see cref="Domain.Exceptions.EstoqueInsuficienteException"/> se algum item não tiver saldo.
        /// </summary>
        Task ReservarAsync(IReadOnlyCollection<ReservaEstoque> reservas, DateTime agora, CancellationToken ct = default);

        /// <summary>Devolve reservas ao saldo disponível (compensação).</summary>
        Task LiberarAsync(IReadOnlyCollection<ReservaEstoque> reservas, DateTime agora, CancellationToken ct = default);

        /// <summary>Transforma reservas em baixa: tira da quantidade atual e da reservada, numa transação.</summary>
        Task BaixarAsync(IReadOnlyCollection<ReservaEstoque> reservas, DateTime agora, CancellationToken ct = default);
    }
}
