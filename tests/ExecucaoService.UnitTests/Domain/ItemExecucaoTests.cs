using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;

namespace ExecucaoService.UnitTests.Domain
{
    public class ItemExecucaoTests
    {
        [Fact]
        public void Peca_consome_estoque_e_calcula_total()
        {
            var item = new ItemExecucao(" Pastilha ", TipoItem.Peca, 2, 89.905m, 15, null);

            Assert.True(item.ConsomeEstoque);
            Assert.Equal("Pastilha", item.Descricao);
            Assert.Equal(89.90m, item.ValorUnitario);
            Assert.Equal(179.80m, item.ValorTotal);
        }

        [Fact]
        public void Servico_nao_consome_estoque()
        {
            Assert.False(new ItemExecucao("Troca", TipoItem.Servico, 1, 120m, null, 3).ConsomeEstoque);
        }

        [Theory]
        [InlineData("", TipoItem.Peca, 1, 1, 1, null)]
        [InlineData("x", (TipoItem)7, 1, 1, 1, null)]
        [InlineData("x", TipoItem.Peca, 0, 1, 1, null)]
        [InlineData("x", TipoItem.Peca, 1, -1, 1, null)]
        [InlineData("x", TipoItem.Peca, 1, 1, null, null)]
        [InlineData("x", TipoItem.Insumo, 1, 1, 1, 2)]
        [InlineData("x", TipoItem.Servico, 1, 1, null, null)]
        [InlineData("x", TipoItem.Servico, 1, 1, 4, 2)]
        public void Recusa_item_invalido(string descricao, TipoItem tipo, int quantidade, double valor, int? estoqueId, int? servicoId)
        {
            Assert.Throws<RegraDeNegocioException>(() => new ItemExecucao(descricao, tipo, quantidade, (decimal)valor, estoqueId, servicoId));
        }

        [Fact]
        public void Descricao_longa_demais_e_recusada()
        {
            Assert.Throws<RegraDeNegocioException>(() => new ItemExecucao(new string('x', 1001), TipoItem.Peca, 1, 1, 1, null));
        }
    }
}
