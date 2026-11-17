namespace ExecucaoService.Application.Interfaces
{
    /// <summary>Hora atual em UTC; existe para os testes controlarem o tempo.</summary>
    public interface IRelogio
    {
        DateTime UtcAgora { get; }
    }

    public sealed class RelogioSistema : IRelogio
    {
        public DateTime UtcAgora => DateTime.UtcNow;
    }
}
