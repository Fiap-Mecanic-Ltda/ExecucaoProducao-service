using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.DTOs;
using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Domain.Exceptions;
using ExecucaoService.Testes.Compartilhado;

namespace ExecucaoService.UnitTests.Application
{
    public class ExecucaoAppServiceTests
    {
        private static readonly Guid Saga = Guid.Parse("3f0c2f6e-8a4b-4c1e-9a52-0b6f1d2e7c11");

        private readonly ExecucoesEmMemoria _execucoes = new();
        private readonly EstoqueEmMemoria _estoque = new();
        private readonly ServicosEmMemoria _servicos = new();
        private readonly RelogioFixo _relogio = new();
        private readonly ExecucaoAppService _app;

        private const int Pastilha = 1;
        private const int Fluido = 2;
        private const int TrocaPastilhas = 1;

        public ExecucaoAppServiceTests()
        {
            _app = new ExecucaoAppService(_execucoes, _estoque, _servicos, _relogio);

            _estoque.AdicionarAsync(Estoque.Criar(Pastilha, "Pastilha", TipoEstoque.Peca, 5, 1, _relogio.UtcAgora)).Wait();
            _estoque.AdicionarAsync(Estoque.Criar(Fluido, "Fluido de freio", TipoEstoque.Insumo, 3, 1, _relogio.UtcAgora)).Wait();
            _servicos.SalvarAsync(ServicoOficina.Criar(TrocaPastilhas, "Troca de pastilhas", null, 120m, _relogio.UtcAgora)).Wait();
        }

        private static DiagnosticoDto Diagnostico(int pastilhas = 2) => new(
        [
            new ItemDiagnosticoDto("Pastilha dianteira", TipoItem.Peca, pastilhas, 89.90m, Pastilha, null),
            new ItemDiagnosticoDto(null, TipoItem.Insumo, 1, 30m, Fluido, null),
            new ItemDiagnosticoDto(null, TipoItem.Servico, 1, null, null, TrocaPastilhas),
        ], "Discos ok");

        private async Task<int> Disponivel(int id) => (await _estoque.ObterAsync(id))!.QuantidadeDisponivel;
        private async Task<int> Atual(int id) => (await _estoque.ObterAsync(id))!.QuantidadeAtual;

        private async Task<ExecucaoDto> ComDiagnosticoAsync(int os = 42, int pastilhas = 2)
        {
            await _app.ReceberAsync(os, Saga, 7, 3, "Freio chiando");
            return await _app.RegistrarDiagnosticoAsync(os, Diagnostico(pastilhas));
        }

        [Fact]
        public async Task Receber_cria_a_execucao_aguardando_diagnostico()
        {
            var dto = await _app.ReceberAsync(42, Saga, 7, 3, "Freio chiando");

            Assert.Equal(StatusExecucao.AguardandoDiagnostico, dto.Status);
            Assert.Equal(Saga, dto.CorrelationId);
            Assert.Single(dto.Historico);
            Assert.NotNull(await _execucoes.ObterAsync(42));
        }

        [Fact]
        public async Task Receber_de_novo_nao_muda_a_execucao()
        {
            await _app.ReceberAsync(42, Saga, 7, 3, "Freio chiando");
            await _app.IniciarDiagnosticoAsync(42);

            var dto = await _app.ReceberAsync(42, Guid.NewGuid(), 7, 3, "Outro texto");

            Assert.Equal(StatusExecucao.EmDiagnostico, dto.Status);
            Assert.Equal(Saga, dto.CorrelationId);
        }

        [Fact]
        public async Task Receber_com_diagnostico_concluido_reabre_e_devolve_reservas()
        {
            await ComDiagnosticoAsync();
            Assert.Equal(3, await Disponivel(Pastilha));

            var dto = await _app.ReceberAsync(42, Saga, 7, 3, null);

            Assert.Equal(StatusExecucao.EmDiagnostico, dto.Status);
            Assert.Empty(dto.Itens);
            Assert.Equal(5, await Disponivel(Pastilha));
            Assert.Equal(3, await Disponivel(Fluido));
        }

        [Fact]
        public async Task Receber_cancelada_recomeca_a_saga()
        {
            await _app.ReceberAsync(42, Saga, 7, 3, null);
            await _app.CancelarAsync(42, "Orcamento recusado");
            var novaSaga = Guid.NewGuid();

            var dto = await _app.ReceberAsync(42, novaSaga, 7, 3, "Voltou");

            Assert.Equal(StatusExecucao.AguardandoDiagnostico, dto.Status);
            Assert.Equal(novaSaga, dto.CorrelationId);
        }

        [Fact]
        public async Task Receber_em_execucao_lanca_regra()
        {
            await ComDiagnosticoAsync();
            await _app.LiberarParaExecucaoAsync(42);

            await Assert.ThrowsAsync<RegraDeNegocioException>(() => _app.ReceberAsync(42, Saga, 7, 3, null));
        }

        [Fact]
        public async Task Diagnostico_reserva_estoque_e_completa_servico_pelo_catalogo()
        {
            var dto = await ComDiagnosticoAsync();

            Assert.Equal(StatusExecucao.DiagnosticoConcluido, dto.Status);
            Assert.Equal(3, dto.Itens.Count);
            var servico = dto.Itens.Single(i => i.Tipo == TipoItem.Servico);
            Assert.Equal("Troca de pastilhas", servico.Descricao);
            Assert.Equal(120m, servico.ValorUnitario);
            Assert.Equal("Fluido de freio", dto.Itens.Single(i => i.Tipo == TipoItem.Insumo).Descricao);
            Assert.Equal(2 * 89.90m + 30m + 120m, dto.ValorTotalItens);
            Assert.Equal(3, await Disponivel(Pastilha));
            Assert.Equal(5, await Atual(Pastilha));
            Assert.Equal(2, await Disponivel(Fluido));
        }

        [Fact]
        public async Task Diagnostico_sem_saldo_nao_reserva_nada_nem_grava()
        {
            await _app.ReceberAsync(42, Saga, 7, 3, null);

            await Assert.ThrowsAsync<EstoqueInsuficienteException>(() => _app.RegistrarDiagnosticoAsync(42, Diagnostico(pastilhas: 6)));

            Assert.Equal(5, await Disponivel(Pastilha));
            Assert.Equal(3, await Disponivel(Fluido));
            Assert.Equal(StatusExecucao.AguardandoDiagnostico, (await _app.ObterAsync(42)).Status);
        }

        [Fact]
        public async Task Diagnostico_que_falha_ao_gravar_devolve_a_reserva()
        {
            await _app.ReceberAsync(42, Saga, 7, 3, null);
            _execucoes.FalharProximaGravacao = new ConflitoDeConcorrenciaException("outro processo gravou antes");

            await Assert.ThrowsAsync<ConflitoDeConcorrenciaException>(() => _app.RegistrarDiagnosticoAsync(42, Diagnostico()));

            Assert.Equal(5, await Disponivel(Pastilha));
            Assert.Equal(3, await Disponivel(Fluido));
        }

        [Fact]
        public async Task Diagnostico_vazio_lanca_regra()
        {
            await _app.ReceberAsync(42, Saga, 7, 3, null);

            await Assert.ThrowsAsync<RegraDeNegocioException>(() => _app.RegistrarDiagnosticoAsync(42, new DiagnosticoDto([], null)));
        }

        [Fact]
        public async Task Diagnostico_com_estoque_inexistente_lanca_regra()
        {
            await _app.ReceberAsync(42, Saga, 7, 3, null);
            var dto = new DiagnosticoDto([new ItemDiagnosticoDto("X", TipoItem.Peca, 1, 10m, 99, null)], null);

            await Assert.ThrowsAsync<RegraDeNegocioException>(() => _app.RegistrarDiagnosticoAsync(42, dto));
        }

        [Fact]
        public async Task Diagnostico_com_peca_sem_valor_lanca_regra()
        {
            await _app.ReceberAsync(42, Saga, 7, 3, null);
            var dto = new DiagnosticoDto([new ItemDiagnosticoDto(null, TipoItem.Peca, 1, null, Pastilha, null)], null);

            await Assert.ThrowsAsync<RegraDeNegocioException>(() => _app.RegistrarDiagnosticoAsync(42, dto));
        }

        [Fact]
        public async Task Diagnostico_com_servico_inativo_lanca_regra()
        {
            var servico = (await _servicos.ObterAsync(TrocaPastilhas))!;
            servico.Atualizar(servico.Nome, null, servico.ValorBase, false, _relogio.UtcAgora);
            await _app.ReceberAsync(42, Saga, 7, 3, null);
            var dto = new DiagnosticoDto([new ItemDiagnosticoDto(null, TipoItem.Servico, 1, null, null, TrocaPastilhas)], null);

            await Assert.ThrowsAsync<RegraDeNegocioException>(() => _app.RegistrarDiagnosticoAsync(42, dto));
        }

        [Fact]
        public async Task Diagnostico_com_servico_fora_do_catalogo_lanca_regra()
        {
            await _app.ReceberAsync(42, Saga, 7, 3, null);
            var dto = new DiagnosticoDto([new ItemDiagnosticoDto("Regulagem", TipoItem.Servico, 1, 45m, null, null)], null);

            await Assert.ThrowsAsync<RegraDeNegocioException>(() => _app.RegistrarDiagnosticoAsync(42, dto));
        }

        [Fact]
        public async Task Diagnostico_com_valor_informado_prevalece_sobre_o_catalogo()
        {
            await _app.ReceberAsync(42, Saga, 7, 3, null);
            var dto = new DiagnosticoDto([new ItemDiagnosticoDto("Troca com desconto", TipoItem.Servico, 1, 100m, null, TrocaPastilhas)], null);

            var resultado = await _app.RegistrarDiagnosticoAsync(42, dto);

            Assert.Equal(100m, resultado.ValorTotalItens);
            Assert.Equal("Troca com desconto", Assert.Single(resultado.Itens).Descricao);
        }

        [Fact]
        public async Task Diagnostico_fora_do_status_lanca_regra()
        {
            await ComDiagnosticoAsync();

            await Assert.ThrowsAsync<RegraDeNegocioException>(() => _app.RegistrarDiagnosticoAsync(42, Diagnostico()));
        }

        [Fact]
        public async Task Fluxo_completo_baixa_o_estoque_e_finaliza()
        {
            await ComDiagnosticoAsync();

            var naFila = await _app.LiberarParaExecucaoAsync(42);
            Assert.Equal(StatusExecucao.NaFila, naFila.Status);
            Assert.Equal(3, await Atual(Pastilha));
            Assert.Equal(3, await Disponivel(Pastilha));
            Assert.Equal(2, await Atual(Fluido));

            _relogio.Avancar(TimeSpan.FromMinutes(5));
            var emExecucao = await _app.IniciarReparoAsync(42);
            Assert.Equal(StatusExecucao.EmExecucao, emExecucao.Status);
            Assert.NotNull(emExecucao.DataInicio);

            _relogio.Avancar(TimeSpan.FromHours(1));
            var finalizada = await _app.FinalizarAsync(42, "Testado na rua");
            Assert.Equal(StatusExecucao.Finalizada, finalizada.Status);
            Assert.Equal(_relogio.UtcAgora, finalizada.DataFim);
            Assert.Equal(6, finalizada.Historico.Count);
        }

        [Fact]
        public async Task Liberar_sem_diagnostico_lanca_regra_e_nao_baixa()
        {
            await _app.ReceberAsync(42, Saga, 7, 3, null);

            await Assert.ThrowsAsync<RegraDeNegocioException>(() => _app.LiberarParaExecucaoAsync(42));
            Assert.Equal(5, await Atual(Pastilha));
        }

        [Fact]
        public async Task Cancelar_devolve_reservas()
        {
            await ComDiagnosticoAsync();

            var dto = await _app.CancelarAsync(42, null);

            Assert.Equal(StatusExecucao.Cancelada, dto!.Status);
            Assert.NotNull(dto.MotivoCancelamento);
            Assert.Equal(5, await Disponivel(Pastilha));
            Assert.Equal(3, await Disponivel(Fluido));
        }

        [Fact]
        public async Task Cancelar_duas_vezes_nao_devolve_em_dobro()
        {
            await ComDiagnosticoAsync(os: 42);
            await ComDiagnosticoAsync(os: 43);

            await _app.CancelarAsync(42, "recusado");
            await _app.CancelarAsync(42, "recusado");

            Assert.Equal(3, await Disponivel(Pastilha));
        }

        [Fact]
        public async Task Cancelar_inexistente_devolve_nulo()
        {
            Assert.Null(await _app.CancelarAsync(99, null));
        }

        [Fact]
        public async Task Cancelar_finalizada_lanca_regra()
        {
            await ComDiagnosticoAsync();
            await _app.LiberarParaExecucaoAsync(42);
            await _app.IniciarReparoAsync(42);
            await _app.FinalizarAsync(42, null);

            await Assert.ThrowsAsync<RegraDeNegocioException>(() => _app.CancelarAsync(42, null));
        }

        [Fact]
        public async Task Obter_inexistente_lanca_nao_encontrado()
        {
            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => _app.ObterAsync(99));
        }

        [Fact]
        public async Task Fila_segue_a_ordem_de_atendimento()
        {
            await _app.ReceberAsync(1, Guid.NewGuid(), null, null, null);       // aguardando
            _relogio.Avancar(TimeSpan.FromMinutes(1));
            await _app.ReceberAsync(2, Guid.NewGuid(), null, null, null);
            await _app.IniciarDiagnosticoAsync(2);                               // em diagnóstico
            _relogio.Avancar(TimeSpan.FromMinutes(1));
            await ComDiagnosticoAsync(os: 3, pastilhas: 1);
            await _app.LiberarParaExecucaoAsync(3);                              // na fila
            _relogio.Avancar(TimeSpan.FromMinutes(1));
            await ComDiagnosticoAsync(os: 4, pastilhas: 1);
            await _app.LiberarParaExecucaoAsync(4);
            await _app.IniciarReparoAsync(4);                                    // em execução
            await ComDiagnosticoAsync(os: 5, pastilhas: 1);                      // diagnóstico concluído
            await _app.ReceberAsync(6, Guid.NewGuid(), null, null, null);
            await _app.CancelarAsync(6, null);                                   // fora da fila

            var fila = await _app.ListarFilaAsync();

            Assert.Equal([4, 3, 2, 1, 5], fila.Select(f => f.OrdemServicoId));
            Assert.Equal(1, Assert.Single(await _app.ListarFilaAsync(StatusExecucao.AguardandoDiagnostico)).OrdemServicoId);
        }

        [Fact]
        public async Task Fila_com_status_invalido_lanca_regra()
        {
            await Assert.ThrowsAsync<RegraDeNegocioException>(() => _app.ListarFilaAsync((StatusExecucao)99));
        }
    }
}
