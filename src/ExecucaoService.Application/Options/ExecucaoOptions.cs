namespace ExecucaoService.Application.Options
{
    /// <summary>Seção "Execucao".</summary>
    public class ExecucaoOptions
    {
        public const string Secao = "Execucao";

        /// <summary>
        /// Demonstração: diagnóstico e reparo andam sozinhos, sem um mecânico chamando a API.
        /// Desligado por padrão.
        /// </summary>
        public bool ModoAutomatico { get; set; }

        /// <summary>Quanto tempo a OS fica em cada passo antes de o modo automático avançar.</summary>
        public int SegundosPorPasso { get; set; } = 3;

        /// <summary>Valor unitário das peças e insumos no diagnóstico automático (o estoque não guarda preço).</summary>
        public decimal ValorPecaAutomatico { get; set; } = 100m;
    }
}
