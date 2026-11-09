namespace ExecucaoService.Domain.Enums
{
    public static class StatusExecucaoExtensions
    {
        private static readonly Dictionary<StatusExecucao, StatusExecucao[]> Transicoes = new()
        {
            [StatusExecucao.AguardandoDiagnostico] = [StatusExecucao.EmDiagnostico, StatusExecucao.Cancelada],
            // DiagnosticoConcluido -> EmDiagnostico: o OS Service reenvia IniciarDiagnostico
            // depois de um OrcamentoFalhou e o diagnóstico é refeito.
            [StatusExecucao.EmDiagnostico]         = [StatusExecucao.DiagnosticoConcluido, StatusExecucao.Cancelada],
            [StatusExecucao.DiagnosticoConcluido]  = [StatusExecucao.NaFila, StatusExecucao.EmDiagnostico, StatusExecucao.Cancelada],
            [StatusExecucao.NaFila]                = [StatusExecucao.EmExecucao, StatusExecucao.Cancelada],
            [StatusExecucao.EmExecucao]            = [StatusExecucao.Finalizada],
            [StatusExecucao.Finalizada]            = [],
            [StatusExecucao.Cancelada]             = []
        };

        /// <summary>Indica se a OS pode passar de <paramref name="atual"/> para <paramref name="novo"/>.</summary>
        public static bool PodeIrPara(this StatusExecucao atual, StatusExecucao novo)
            => Transicoes.TryGetValue(atual, out var permitidos) && permitidos.Contains(novo);

        /// <summary>Estados finais não aceitam mais transições.</summary>
        public static bool EhFinal(this StatusExecucao status)
            => status is StatusExecucao.Finalizada or StatusExecucao.Cancelada;
    }
}
