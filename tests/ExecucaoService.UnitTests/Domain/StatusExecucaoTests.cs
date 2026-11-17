using ExecucaoService.Domain.Enums;

namespace ExecucaoService.UnitTests.Domain
{
    public class StatusExecucaoTests
    {
        [Theory]
        [InlineData(StatusExecucao.AguardandoDiagnostico, StatusExecucao.EmDiagnostico)]
        [InlineData(StatusExecucao.EmDiagnostico, StatusExecucao.DiagnosticoConcluido)]
        [InlineData(StatusExecucao.DiagnosticoConcluido, StatusExecucao.NaFila)]
        [InlineData(StatusExecucao.NaFila, StatusExecucao.EmExecucao)]
        [InlineData(StatusExecucao.EmExecucao, StatusExecucao.Finalizada)]
        public void Caminho_feliz_e_permitido(StatusExecucao atual, StatusExecucao novo)
        {
            Assert.True(atual.PodeIrPara(novo));
        }

        [Theory]
        [InlineData(StatusExecucao.AguardandoDiagnostico)]
        [InlineData(StatusExecucao.EmDiagnostico)]
        [InlineData(StatusExecucao.DiagnosticoConcluido)]
        [InlineData(StatusExecucao.NaFila)]
        public void Pode_cancelar_antes_do_reparo_comecar(StatusExecucao atual)
        {
            Assert.True(atual.PodeIrPara(StatusExecucao.Cancelada));
        }

        [Fact]
        public void Reparo_em_andamento_nao_e_cancelado()
        {
            Assert.False(StatusExecucao.EmExecucao.PodeIrPara(StatusExecucao.Cancelada));
        }

        [Fact]
        public void Diagnostico_concluido_pode_ser_refeito()
        {
            Assert.True(StatusExecucao.DiagnosticoConcluido.PodeIrPara(StatusExecucao.EmDiagnostico));
        }

        [Theory]
        [InlineData(StatusExecucao.AguardandoDiagnostico, StatusExecucao.NaFila)]
        [InlineData(StatusExecucao.EmDiagnostico, StatusExecucao.EmExecucao)]
        [InlineData(StatusExecucao.NaFila, StatusExecucao.Finalizada)]
        public void Nao_pula_etapas(StatusExecucao atual, StatusExecucao novo)
        {
            Assert.False(atual.PodeIrPara(novo));
        }

        [Theory]
        [InlineData(StatusExecucao.Finalizada)]
        [InlineData(StatusExecucao.Cancelada)]
        public void Estados_finais_nao_tem_saida(StatusExecucao final)
        {
            Assert.True(final.EhFinal());
            Assert.All(Enum.GetValues<StatusExecucao>(), novo => Assert.False(final.PodeIrPara(novo)));
        }

        [Fact]
        public void Status_desconhecido_nao_transita()
        {
            Assert.False(((StatusExecucao)99).PodeIrPara(StatusExecucao.Finalizada));
        }
    }
}
