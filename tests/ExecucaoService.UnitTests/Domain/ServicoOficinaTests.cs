using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Exceptions;

namespace ExecucaoService.UnitTests.Domain
{
    public class ServicoOficinaTests
    {
        private static readonly DateTime Agora = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Criar_arredonda_o_valor_e_comeca_ativo()
        {
            var s = ServicoOficina.Criar(1, " Alinhamento ", "  ", 99.999m, Agora);

            Assert.Equal("Alinhamento", s.Nome);
            Assert.Null(s.Descricao);
            Assert.Equal(100.00m, s.ValorBase);
            Assert.True(s.Ativo);
        }

        [Fact]
        public void Atualizar_pode_inativar()
        {
            var s = ServicoOficina.Criar(1, "Alinhamento", null, 80m, Agora);

            s.Atualizar("Alinhamento 3D", "Com balanceamento", 90m, false, Agora.AddHours(1));

            Assert.False(s.Ativo);
            Assert.Equal("Com balanceamento", s.Descricao);
            Assert.Equal(Agora.AddHours(1), s.DataAtualizacao);
        }

        [Theory]
        [InlineData("", 10)]
        [InlineData("Alinhamento", -1)]
        public void Recusa_dados_invalidos(string nome, decimal valor)
        {
            Assert.Throws<RegraDeNegocioException>(() => ServicoOficina.Criar(1, nome, null, valor, Agora));
        }

        [Fact]
        public void Recusa_textos_longos_demais()
        {
            Assert.Throws<RegraDeNegocioException>(() => ServicoOficina.Criar(1, new string('x', 201), null, 1, Agora));
            Assert.Throws<RegraDeNegocioException>(() => ServicoOficina.Criar(1, "Ok", new string('x', 1001), 1, Agora));
        }
    }
}
