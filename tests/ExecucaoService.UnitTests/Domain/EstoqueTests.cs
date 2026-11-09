using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;

namespace ExecucaoService.UnitTests.Domain
{
    public class EstoqueTests
    {
        private static readonly DateTime Agora = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Criar_preenche_o_cadastro_sem_reserva()
        {
            var e = Estoque.Criar(1, "  Pastilha  ", TipoEstoque.Peca, 5, 1, Agora);

            Assert.Equal("Pastilha", e.Nome);
            Assert.Equal(5, e.QuantidadeDisponivel);
            Assert.Equal(0, e.QuantidadeReservada);
            Assert.Equal(Agora, e.DataUltimaAtualizacao);
        }

        [Theory]
        [InlineData("", TipoEstoque.Peca, 1, 0)]
        [InlineData("Pastilha", (TipoEstoque)9, 1, 0)]
        [InlineData("Pastilha", TipoEstoque.Peca, -1, 0)]
        [InlineData("Pastilha", TipoEstoque.Peca, 1, -1)]
        public void Criar_recusa_cadastro_invalido(string nome, TipoEstoque tipo, int atual, int minima)
        {
            Assert.Throws<RegraDeNegocioException>(() => Estoque.Criar(1, nome, tipo, atual, minima, Agora));
        }

        [Fact]
        public void Nome_longo_demais_e_recusado()
        {
            Assert.Throws<RegraDeNegocioException>(() => Estoque.Criar(1, new string('x', 201), TipoEstoque.Insumo, 1, 0, Agora));
        }

        [Fact]
        public void Baixo_estoque_considera_o_saldo_disponivel()
        {
            var e = Estoque.Restaurar(1, "Óleo", TipoEstoque.Insumo, 10, 8, 2, Agora);

            Assert.Equal(2, e.QuantidadeDisponivel);
            Assert.True(e.EstaBaixoEstoque());
        }

        [Fact]
        public void Item_com_reserva_nao_pode_ser_removido()
        {
            var e = Estoque.Restaurar(1, "Óleo", TipoEstoque.Insumo, 10, 1, 2, Agora);

            Assert.Throws<RegraDeNegocioException>(e.ValidarRemocao);
        }

        [Fact]
        public void Item_sem_reserva_pode_ser_removido()
        {
            Estoque.Restaurar(1, "Óleo", TipoEstoque.Insumo, 10, 0, 2, Agora).ValidarRemocao();
        }
    }
}
