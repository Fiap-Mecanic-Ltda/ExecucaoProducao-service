using ExecucaoService.Domain.Enums;
using ExecucaoService.Infrastructure.Integracoes;
using System.Net;
using System.Text;

namespace ExecucaoService.UnitTests.Infrastructure
{
    public class CatalogoMonolitoClientTests
    {
        private sealed class HandlerFalso(HttpStatusCode status, string corpo) : HttpMessageHandler
        {
            public List<HttpRequestMessage> Recebidas { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Recebidas.Add(request);
                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") });
            }
        }

        private static (CatalogoMonolitoClient Cliente, HandlerFalso Handler) Criar(HttpStatusCode status, string corpo)
        {
            var handler = new HandlerFalso(status, corpo);
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://monolito:8080/") };
            return (new CatalogoMonolitoClient(http, "tok"), handler);
        }

        [Fact]
        public async Task Le_o_estoque_no_formato_do_MechanicLtda_com_o_token()
        {
            var (cliente, handler) = Criar(HttpStatusCode.OK,
                """[{"id":7,"nome":"Pastilha","tipo":2,"quantidadeAtual":12,"quantidadeMinima":3,"dataUltimaAtualizacao":"2026-11-08T10:00:00Z","baixoEstoque":false}]""");

            var itens = await cliente.ObterEstoqueAsync();

            var item = Assert.Single(itens);
            Assert.Equal((7, "Pastilha", TipoEstoque.Peca, 12, 3), (item.Id, item.Nome, item.Tipo, item.QuantidadeAtual, item.QuantidadeMinima));
            var requisicao = Assert.Single(handler.Recebidas);
            Assert.Equal("http://monolito:8080/api/estoque", requisicao.RequestUri!.ToString());
            Assert.Equal("Bearer tok", requisicao.Headers.Authorization!.ToString());
        }

        [Fact]
        public async Task Le_os_servicos()
        {
            var (cliente, handler) = Criar(HttpStatusCode.OK,
                """[{"id":2,"nome":"Alinhamento","descricao":null,"valorBase":80.5,"ativo":false,"dataCadastro":"2026-01-01T00:00:00Z","dataAtualizacao":null}]""");

            var servico = Assert.Single(await cliente.ObterServicosAsync());

            Assert.Equal((2, "Alinhamento", 80.5m, false), (servico.Id, servico.Nome, servico.ValorBase, servico.Ativo));
            Assert.EndsWith("/api/servicos-oficina", handler.Recebidas[0].RequestUri!.ToString());
        }

        [Fact]
        public async Task Resposta_de_erro_explica_o_que_conferir()
        {
            var (cliente, _) = Criar(HttpStatusCode.Forbidden, "");

            var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => cliente.ObterEstoqueAsync());

            Assert.Contains("403", erro.Message);
        }
    }
}
