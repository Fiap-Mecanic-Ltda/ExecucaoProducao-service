using ExecucaoService.API.ViewModels;
using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.DTOs;
using ExecucaoService.Bootstrap.Authorization;
using ExecucaoService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExecucaoService.API.Controllers
{
    /// <summary>
    /// Fila de execução: o que a oficina tem para diagnosticar e reparar. As entradas chegam pela
    /// Saga (IniciarDiagnostico e IniciarExecucao); as rotas marcadas como manuais fazem o mesmo
    /// papel enquanto a Saga não está ligada. [Admin]
    /// </summary>
    [Route("api/execucao")]
    [Authorize(Roles = Roles.Admin)]
    public class ExecucaoController : BaseApiController
    {
        private readonly IExecucaoAppService _app;

        public ExecucaoController(IExecucaoAppService app) => _app = app;

        /// <summary>OS em andamento na ordem de atendimento; com <c>status</c>, só as daquele status.</summary>
        [HttpGet("fila")]
        public async Task<IActionResult> Fila([FromQuery] StatusExecucao? status, CancellationToken ct) =>
            Ok(await _app.ListarFilaAsync(status, ct));

        /// <summary>A OS na Execução, com itens do diagnóstico e histórico de status.</summary>
        [HttpGet("{ordemServicoId:int}")]
        public async Task<IActionResult> Obter(int ordemServicoId, CancellationToken ct) => Ok(await _app.ObterAsync(ordemServicoId, ct));

        /// <summary>Manual: coloca uma OS na fila de diagnóstico (o que o comando IniciarDiagnostico faz).</summary>
        [HttpPost]
        public async Task<IActionResult> Receber([FromBody] EntradaManualViewModel model, CancellationToken ct)
        {
            var execucao = await _app.ReceberAsync(model.OrdemServicoId, Guid.Empty, model.ClienteId, model.VeiculoId, model.DescricaoProblema, ct);
            return CreatedAtAction(nameof(Obter), new { ordemServicoId = execucao.OrdemServicoId }, execucao);
        }

        /// <summary>Um mecânico assume o diagnóstico.</summary>
        [HttpPatch("{ordemServicoId:int}/iniciar-diagnostico")]
        public async Task<IActionResult> IniciarDiagnostico(int ordemServicoId, CancellationToken ct) =>
            Ok(await _app.IniciarDiagnosticoAsync(ordemServicoId, ct));

        /// <summary>Registra peças, insumos e serviços, reserva o estoque e conclui o diagnóstico.</summary>
        [HttpPost("{ordemServicoId:int}/diagnostico")]
        public async Task<IActionResult> RegistrarDiagnostico(int ordemServicoId, [FromBody] DiagnosticoViewModel model, CancellationToken ct)
        {
            var itens = model.Itens
                .Select(i => new ItemDiagnosticoDto(i.Descricao, i.Tipo, i.Quantidade, i.ValorUnitario, i.EstoqueId, i.ServicoOficinaId))
                .ToList();

            return Ok(await _app.RegistrarDiagnosticoAsync(ordemServicoId, new DiagnosticoDto(itens, model.Observacao), ct));
        }

        /// <summary>Manual: pagamento confirmado (o que o comando IniciarExecucao faz). Baixa o estoque reservado.</summary>
        [HttpPatch("{ordemServicoId:int}/liberar-execucao")]
        public async Task<IActionResult> LiberarExecucao(int ordemServicoId, CancellationToken ct) =>
            Ok(await _app.LiberarParaExecucaoAsync(ordemServicoId, ct));

        /// <summary>Um mecânico começa o reparo.</summary>
        [HttpPatch("{ordemServicoId:int}/iniciar")]
        public async Task<IActionResult> Iniciar(int ordemServicoId, CancellationToken ct) =>
            Ok(await _app.IniciarReparoAsync(ordemServicoId, ct));

        [HttpPatch("{ordemServicoId:int}/finalizar")]
        public async Task<IActionResult> Finalizar(int ordemServicoId, [FromBody] FinalizacaoViewModel? model, CancellationToken ct) =>
            Ok(await _app.FinalizarAsync(ordemServicoId, model?.Observacao, ct));

        /// <summary>
        /// O diagnóstico não pode ser feito (ex.: peça em falta): devolve as reservas, cancela a OS
        /// na Execução e avisa a Saga com DiagnosticoFalhou (a OS volta para Recebida).
        /// </summary>
        [HttpPatch("{ordemServicoId:int}/falha-diagnostico")]
        public async Task<IActionResult> FalhaDiagnostico(int ordemServicoId, [FromBody] FalhaDiagnosticoViewModel model, CancellationToken ct) =>
            Ok(await _app.RegistrarFalhaDiagnosticoAsync(ordemServicoId, model.Motivo, ct));

        /// <summary>Manual: devolve as reservas e cancela (o que o comando LiberarReservaEstoque faz).</summary>
        [HttpPatch("{ordemServicoId:int}/cancelar")]
        public async Task<IActionResult> Cancelar(int ordemServicoId, [FromBody] CancelamentoViewModel? model, CancellationToken ct)
        {
            var execucao = await _app.CancelarAsync(ordemServicoId, model?.Motivo, ct);
            return execucao is null ? NotFound(new { success = false, errors = new[] { $"A OS {ordemServicoId} não está na Execução." } }) : Ok(execucao);
        }
    }
}
