namespace ExecucaoService.Domain.Enums
{
    /// <summary>
    /// Status de uma OS dentro da Execução. São independentes dos status do OS Service: o OS
    /// Service só conhece o que a Execução publica nos eventos da Saga.
    /// </summary>
    public enum StatusExecucao
    {
        /// <summary>Recebeu IniciarDiagnostico e espera um mecânico.</summary>
        AguardandoDiagnostico = 1,

        /// <summary>Mecânico registrando peças, insumos e serviços.</summary>
        EmDiagnostico = 2,

        /// <summary>Itens registrados e estoque reservado; DiagnosticoConcluido publicado.</summary>
        DiagnosticoConcluido = 3,

        /// <summary>Recebeu IniciarExecucao (pagamento confirmado) e o estoque foi baixado.</summary>
        NaFila = 4,

        /// <summary>Reparo em andamento; ExecucaoIniciada publicado.</summary>
        EmExecucao = 5,

        /// <summary>Reparo concluído; ExecucaoFinalizada publicado.</summary>
        Finalizada = 6,

        /// <summary>Compensada: reservas devolvidas ao estoque.</summary>
        Cancelada = 7
    }
}
