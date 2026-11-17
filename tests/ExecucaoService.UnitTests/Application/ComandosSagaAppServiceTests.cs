using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.DTOs;
using ExecucaoService.Domain.Entities;
using ExecucaoService.Domain.Enums;
using ExecucaoService.Testes.Compartilhado;
using MechanicLtda.Contratos;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExecucaoService.UnitTests.Application
{
    public class ComandosSagaAppServiceTests
    {
        private static readonly Guid Saga = Guid.Parse("3f0c2f6e-8a4b-4c1e-9a52-0b6f1d2e7c11");
        private const int Os = 42;
        private const int Pastilha = 1;

        private readonly ExecucoesEmMemoria _execucoes = new();
        private readonly EstoqueEmMemoria _estoque = new();
        private readonly ServicosEmMemoria _servicos = new();
        private readonly RelogioFixo _relogio = new();
        private readonly PublicadorEmMemoria _publicador = new();
        private readonly MonitoramentoEmMemoria _monitoramento = new();
        private readonly RegistroMensagensEmMemoria _registro = new();
        private readonly ExecucaoAppService _execucao;
        private readonly ComandosSagaAppService _saga;

        public ComandosSagaAppServiceTests()
        {
            _execucao = new ExecucaoAppService(_execucoes, _estoque, _servicos, _relogio, _publicador, _monitoramento);
            _saga = new ComandosSagaAppService(_execucao, _execucoes, _registro, _publicador, _monitoramento, _relogio,
                                               NullLogger<ComandosSagaAppService>.Instance);

            _estoque.AdicionarAsync(Estoque.Criar(Pastilha, "Pastilha", TipoEstoque.Peca, 5, 1, _relogio.UtcAgora)).Wait();
        }

        private IniciarDiagnostico ComandoDiagnostico(string descricao = "Freio chiando") => new()
        {
            CorrelationId = Saga, OrdemServicoId = Os, DataHora = _relogio.UtcAgora,
            ClienteId = 7, VeiculoId = 3, DescricaoProblema = descricao,
        };

        private IniciarExecucao ComandoExecucao() => new() { CorrelationId = Saga, OrdemServicoId = Os, DataHora = _relogio.UtcAgora, OrcamentoId = 1042 };

        private LiberarReservaEstoque ComandoLiberar() => new() { CorrelationId = Saga, OrdemServicoId = Os, DataHora = _relogio.UtcAgora, Motivo = "Orcamento recusado" };

        private async Task DiagnosticarAsync(int pastilhas = 2)
        {
            await _saga.TratarAsync(ComandoDiagnostico());
            await _execucao.RegistrarDiagnosticoAsync(Os, new DiagnosticoDto(
                [new ItemDiagnosticoDto("Pastilha", TipoItem.Peca, pastilhas, 90m, Pastilha, null)], null));
        }

        private async Task<StatusExecucao> StatusAsync() => (await _execucoes.ObterAsync(Os))!.Status;
        private async Task<Estoque> PastilhaAsync() => (await _estoque.ObterAsync(Pastilha))!;

        [Fact]
        public async Task IniciarDiagnostico_poe_a_os_na_fila_com_a_saga()
        {
            await _saga.TratarAsync(ComandoDiagnostico());

            var execucao = (await _execucoes.ObterAsync(Os))!;
            Assert.Equal(StatusExecucao.AguardandoDiagnostico, execucao.Status);
            Assert.Equal(Saga, execucao.CorrelationId);
            Assert.Equal(7, execucao.ClienteId);
            Assert.Equal(1, _registro.Quantidade);
            Assert.Empty(_publicador.Publicados);
        }

        [Fact]
        public async Task Mensagem_repetida_e_descartada()
        {
            var comando = ComandoDiagnostico();
            await _saga.TratarAsync(comando);
            await _execucao.RegistrarDiagnosticoAsync(Os, new DiagnosticoDto(
                [new ItemDiagnosticoDto("Pastilha", TipoItem.Peca, 2, 90m, Pastilha, null)], null));

            await _saga.TratarAsync(comando);

            Assert.Equal(StatusExecucao.DiagnosticoConcluido, await StatusAsync());
            Assert.Equal(2, (await PastilhaAsync()).QuantidadeReservada);
        }

        [Fact]
        public async Task IniciarDiagnostico_reenviado_reabre_o_diagnostico()
        {
            await DiagnosticarAsync();
            _relogio.Avancar(TimeSpan.FromMinutes(10));

            await _saga.TratarAsync(ComandoDiagnostico());

            Assert.Equal(StatusExecucao.EmDiagnostico, await StatusAsync());
            Assert.Equal(0, (await PastilhaAsync()).QuantidadeReservada);
        }

        [Fact]
        public async Task IniciarDiagnostico_depois_do_pagamento_responde_falha()
        {
            await DiagnosticarAsync();
            await _saga.TratarAsync(ComandoExecucao());
            _relogio.Avancar(TimeSpan.FromMinutes(1));

            await _saga.TratarAsync(ComandoDiagnostico());

            var falha = Assert.Single(_publicador.DoTipo<DiagnosticoFalhou>());
            Assert.Equal(Saga, falha.CorrelationId);
            Assert.Equal(Os, falha.OrdemServicoId);
            Assert.Equal(StatusExecucao.NaFila, await StatusAsync());
        }

        [Fact]
        public async Task IniciarExecucao_baixa_o_estoque_e_poe_na_fila()
        {
            await DiagnosticarAsync();

            await _saga.TratarAsync(ComandoExecucao());

            Assert.Equal(StatusExecucao.NaFila, await StatusAsync());
            var pastilha = await PastilhaAsync();
            Assert.Equal(3, pastilha.QuantidadeAtual);
            Assert.Equal(0, pastilha.QuantidadeReservada);
            Assert.Empty(_publicador.DoTipo<ExecucaoFalhou>());
        }

        [Fact]
        public async Task IniciarExecucao_com_a_os_ja_na_fila_nao_baixa_de_novo()
        {
            await DiagnosticarAsync();
            await _saga.TratarAsync(ComandoExecucao());
            _relogio.Avancar(TimeSpan.FromSeconds(1));

            await _saga.TratarAsync(ComandoExecucao());

            Assert.Equal(3, (await PastilhaAsync()).QuantidadeAtual);
            Assert.Empty(_publicador.DoTipo<ExecucaoFalhou>());
        }

        [Fact]
        public async Task IniciarExecucao_de_os_desconhecida_responde_falha()
        {
            await _saga.TratarAsync(ComandoExecucao());

            var falha = Assert.Single(_publicador.DoTipo<ExecucaoFalhou>());
            Assert.Equal(Saga, falha.CorrelationId);
            Assert.Contains("42:ExecucaoFalhou", _monitoramento.Falhas);
        }

        [Fact]
        public async Task IniciarExecucao_sem_diagnostico_cancela_e_responde_falha()
        {
            await _saga.TratarAsync(ComandoDiagnostico());

            await _saga.TratarAsync(ComandoExecucao());

            Assert.Single(_publicador.DoTipo<ExecucaoFalhou>());
            Assert.Equal(StatusExecucao.Cancelada, await StatusAsync());
        }

        [Fact]
        public async Task IniciarExecucao_sem_saldo_devolve_cancela_e_responde_falha()
        {
            await DiagnosticarAsync();
            // Reserva perdida fora do fluxo (ex.: devolvida à mão): a baixa não tem o que consumir.
            await _estoque.LiberarAsync([new ReservaEstoque(Pastilha, 2)], _relogio.UtcAgora);
            await _estoque.ReservarAsync([new ReservaEstoque(Pastilha, 4)], _relogio.UtcAgora);
            await _estoque.BaixarAsync([new ReservaEstoque(Pastilha, 4)], _relogio.UtcAgora);

            await _saga.TratarAsync(ComandoExecucao());

            Assert.Contains("insuficiente", Assert.Single(_publicador.DoTipo<ExecucaoFalhou>()).Motivo);
            Assert.Equal(StatusExecucao.Cancelada, await StatusAsync());
            Assert.Equal(1, (await PastilhaAsync()).QuantidadeAtual);
        }

        [Fact]
        public async Task LiberarReserva_devolve_e_cancela_sem_responder()
        {
            await DiagnosticarAsync();

            await _saga.TratarAsync(ComandoLiberar());

            Assert.Equal(StatusExecucao.Cancelada, await StatusAsync());
            Assert.Equal(0, (await PastilhaAsync()).QuantidadeReservada);
            Assert.Equal("Orcamento recusado", (await _execucoes.ObterAsync(Os))!.MotivoCancelamento);
            Assert.DoesNotContain(_publicador.Publicados, m => m is DiagnosticoFalhou or ExecucaoFalhou);
        }

        [Fact]
        public async Task LiberarReserva_de_os_desconhecida_ou_em_reparo_nao_faz_nada()
        {
            await _saga.TratarAsync(ComandoLiberar());
            Assert.Null(await _execucoes.ObterAsync(Os));

            await DiagnosticarAsync();
            await _saga.TratarAsync(ComandoExecucao());
            await _execucao.IniciarReparoAsync(Os);
            _relogio.Avancar(TimeSpan.FromSeconds(1));

            await _saga.TratarAsync(ComandoLiberar());

            Assert.Equal(StatusExecucao.EmExecucao, await StatusAsync());
        }

        [Fact]
        public async Task Erro_tecnico_nao_registra_a_mensagem_para_poder_tentar_de_novo()
        {
            _execucoes.FalharProximaGravacao = new TimeoutException("DynamoDB fora");

            await Assert.ThrowsAsync<TimeoutException>(() => _saga.TratarAsync(ComandoDiagnostico()));
            Assert.Equal(0, _registro.Quantidade);

            await _saga.TratarAsync(ComandoDiagnostico());
            Assert.Equal(StatusExecucao.AguardandoDiagnostico, await StatusAsync());
        }
    
        [Fact]
        public async Task Mensagem_repetida_republica_a_mesma_resposta()
        {
            var comando = ComandoExecucao();
            await _saga.TratarAsync(comando);
            _relogio.Avancar(TimeSpan.FromMinutes(1));

            await _saga.TratarAsync(comando);

            var falhas = _publicador.DoTipo<ExecucaoFalhou>();
            Assert.Equal(2, falhas.Count);
            Assert.Equal(falhas[0], falhas[1]);
            Assert.Single(_monitoramento.Falhas);
        }
    }
}
