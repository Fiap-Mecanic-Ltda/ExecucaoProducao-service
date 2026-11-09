using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;

namespace ExecucaoService.UnitTests.Domain
{
    public class ExecucaoOrdemServicoTests
    {
        private static readonly DateTime Agora = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        private static readonly Guid Saga = Guid.Parse("3f0c2f6e-8a4b-4c1e-9a52-0b6f1d2e7c11");

        private static ExecucaoOrdemServico Nova() => ExecucaoOrdemServico.Receber(42, Saga, 7, 3, " Freio chiando ", Agora);

        private static List<ItemExecucao> Itens() =>
        [
            new("Pastilha", TipoItem.Peca, 2, 89.90m, 15, null),
            new("Fluido", TipoItem.Insumo, 1, 30m, 20, null),
            new("Pastilha traseira", TipoItem.Peca, 1, 70m, 15, null),
            new("Troca de pastilhas", TipoItem.Servico, 1, 120m, null, 3),
        ];

        [Fact]
        public void Receber_coloca_na_fila_de_diagnostico()
        {
            var e = Nova();

            Assert.Equal(StatusExecucao.AguardandoDiagnostico, e.Status);
            Assert.Equal(Saga, e.CorrelationId);
            Assert.Equal("Freio chiando", e.DescricaoProblema);
            Assert.Single(e.HistoricoNaoGravado);
            Assert.Null(e.HistoricoNaoGravado[0].StatusAnterior);
        }

        [Fact]
        public void Receber_sem_correlationId_gera_um()
        {
            Assert.NotEqual(Guid.Empty, ExecucaoOrdemServico.Receber(1, Guid.Empty, null, null, null, Agora).CorrelationId);
        }

        [Fact]
        public void Receber_recusa_dados_invalidos()
        {
            Assert.Throws<RegraDeNegocioException>(() => ExecucaoOrdemServico.Receber(0, Saga, null, null, null, Agora));
            Assert.Throws<RegraDeNegocioException>(() => ExecucaoOrdemServico.Receber(1, Saga, null, null, new string('x', 1001), Agora));
        }

        [Fact]
        public void Concluir_diagnostico_soma_reservas_por_item_de_estoque()
        {
            var e = Nova();

            e.ConcluirDiagnostico(Itens(), " ok ", Agora.AddMinutes(30));

            Assert.Equal(StatusExecucao.DiagnosticoConcluido, e.Status);
            Assert.Equal("ok", e.Observacao);
            Assert.Equal(Agora.AddMinutes(30), e.DataConclusaoDiagnostico);
            Assert.Equal([new ReservaEstoque(15, 3), new ReservaEstoque(20, 1)], e.Reservas);
            Assert.Equal(399.80m, e.ValorTotalItens);
            // Passou por EmDiagnostico automaticamente.
            Assert.Equal(3, e.HistoricoNaoGravado.Count);
        }

        [Fact]
        public void Diagnostico_sem_itens_e_recusado()
        {
            Assert.Throws<RegraDeNegocioException>(() => Nova().ConcluirDiagnostico([], null, Agora));
        }

        [Fact]
        public void Observacao_longa_demais_e_recusada()
        {
            Assert.Throws<RegraDeNegocioException>(() => Nova().ConcluirDiagnostico(Itens(), new string('x', 1001), Agora));
            var e = Nova();
            e.ConcluirDiagnostico(Itens(), null, Agora);
            e.EntrarNaFila(Agora);
            e.IniciarReparo(Agora);
            Assert.Throws<RegraDeNegocioException>(() => e.Finalizar(Agora, new string('x', 1001)));
        }

        [Fact]
        public void Caminho_completo_ate_finalizar()
        {
            var e = Nova();
            e.IniciarDiagnostico(Agora.AddMinutes(1));
            e.ConcluirDiagnostico(Itens(), null, Agora.AddMinutes(2));
            e.EntrarNaFila(Agora.AddMinutes(3));

            Assert.Empty(e.Reservas);

            e.IniciarReparo(Agora.AddMinutes(4));
            e.Finalizar(Agora.AddMinutes(5), "Testado na rua");

            Assert.Equal(StatusExecucao.Finalizada, e.Status);
            Assert.Equal(Agora.AddMinutes(4), e.DataInicio);
            Assert.Equal(Agora.AddMinutes(5), e.DataFim);
            Assert.Equal("Testado na rua", e.HistoricoNaoGravado[^1].Observacao);
        }

        [Fact]
        public void Nao_pula_do_diagnostico_para_o_reparo()
        {
            var e = Nova();

            Assert.Throws<RegraDeNegocioException>(() => e.IniciarReparo(Agora));
        }

        [Fact]
        public void Reabrir_diagnostico_limpa_itens_e_reservas()
        {
            var e = Nova();
            e.ConcluirDiagnostico(Itens(), null, Agora);

            e.ReabrirDiagnostico(Agora.AddHours(1));

            Assert.Equal(StatusExecucao.EmDiagnostico, e.Status);
            Assert.Empty(e.Itens);
            Assert.Empty(e.Reservas);
            Assert.Null(e.DataConclusaoDiagnostico);
        }

        [Fact]
        public void Cancelar_registra_o_motivo_e_limpa_reservas()
        {
            var e = Nova();
            e.ConcluirDiagnostico(Itens(), null, Agora);

            e.Cancelar("Orçamento recusado", Agora.AddHours(1));

            Assert.Equal(StatusExecucao.Cancelada, e.Status);
            Assert.Equal("Orçamento recusado", e.MotivoCancelamento);
            Assert.Empty(e.Reservas);
        }

        [Fact]
        public void Cancelar_sem_motivo_e_corta_motivo_longo()
        {
            var a = Nova();
            a.Cancelar(" ", Agora);
            Assert.Equal("Cancelada", a.MotivoCancelamento);

            var b = Nova();
            b.Cancelar(new string('x', 1500), Agora);
            Assert.Equal(1000, b.MotivoCancelamento!.Length);
        }

        [Fact]
        public void Reparo_em_andamento_nao_e_cancelado()
        {
            var e = Nova();
            e.ConcluirDiagnostico(Itens(), null, Agora);
            e.EntrarNaFila(Agora);
            e.IniciarReparo(Agora);

            Assert.Throws<RegraDeNegocioException>(() => e.Cancelar("x", Agora));
        }

        [Fact]
        public void Recomecar_saga_so_depois_de_cancelada()
        {
            var e = Nova();
            Assert.Throws<RegraDeNegocioException>(() => e.RecomecarSaga(Guid.NewGuid(), null, Agora));

            e.ConcluirDiagnostico(Itens(), "obs", Agora);
            e.Cancelar("Prazo vencido", Agora);
            var novaSaga = Guid.NewGuid();

            e.RecomecarSaga(novaSaga, "Barulho no freio", Agora.AddDays(1));

            Assert.Equal(StatusExecucao.AguardandoDiagnostico, e.Status);
            Assert.Equal(novaSaga, e.CorrelationId);
            Assert.Equal("Barulho no freio", e.DescricaoProblema);
            Assert.Empty(e.Itens);
            Assert.Null(e.MotivoCancelamento);
            Assert.Null(e.Observacao);
        }

        [Fact]
        public void Recomecar_saga_sem_descricao_mantem_a_anterior()
        {
            var e = Nova();
            e.Cancelar("x", Agora);

            e.RecomecarSaga(Guid.Empty, null, Agora);

            Assert.Equal("Freio chiando", e.DescricaoProblema);
            Assert.NotEqual(Guid.Empty, e.CorrelationId);
        }

        [Fact]
        public void Marcar_como_gravado_move_o_historico_e_sobe_a_versao()
        {
            var e = Nova();

            e.MarcarComoGravado();

            Assert.Equal(1, e.Versao);
            Assert.Single(e.Historico);
            Assert.Empty(e.HistoricoNaoGravado);
        }

        [Fact]
        public void Restaurar_ordena_o_historico_por_data()
        {
            var e = ExecucaoOrdemServico.Restaurar(1, Saga, null, null, null, StatusExecucao.EmDiagnostico, null, null,
                Agora, Agora, null, null, null, 2, [], [],
                [
                    new HistoricoExecucao(StatusExecucao.AguardandoDiagnostico, StatusExecucao.EmDiagnostico, Agora.AddMinutes(1), null),
                    new HistoricoExecucao(null, StatusExecucao.AguardandoDiagnostico, Agora, null)
                ]);

            Assert.Null(e.Historico[0].StatusAnterior);
            Assert.Equal(2, e.Versao);
        }
    }
}
