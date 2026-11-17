using ExecucaoService.API.ViewModels;
using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.DTOs;
using ExecucaoService.Bootstrap.Authorization;
using ExecucaoService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExecucaoService.API.Controllers
{
    /// <summary>Estoque de peças e insumos. Mesmas rotas do MechanicLtda. [Admin]</summary>
    [Route("api/estoque")]
    [Authorize(Roles = Roles.Admin)]
    public class EstoqueController : BaseApiController
    {
        private readonly IEstoqueAppService _app;

        public EstoqueController(IEstoqueAppService app) => _app = app;

        [HttpGet]
        public async Task<IActionResult> Listar(CancellationToken ct) => Ok(await _app.ListarAsync(null, ct));

        /// <summary>Itens de um tipo (1 = Insumo, 2 = Peça).</summary>
        [HttpGet("tipo/{tipo}")]
        public async Task<IActionResult> ListarPorTipo(TipoEstoque tipo, CancellationToken ct) => Ok(await _app.ListarAsync(tipo, ct));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Obter(int id, CancellationToken ct) => Ok(await _app.ObterAsync(id, ct));

        [HttpPost]
        public async Task<IActionResult> Criar([FromBody] EstoqueCreateViewModel model, CancellationToken ct)
        {
            var criado = await _app.AdicionarAsync(new EstoqueInputDto(model.Nome, model.Tipo, model.QuantidadeAtual, model.QuantidadeMinima), ct);
            return CreatedAtAction(nameof(Obter), new { id = criado.Id }, criado);
        }

        /// <summary>Nome, tipo e quantidade mínima. Quantidades mudam por reposição, reserva e baixa.</summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Atualizar(int id, [FromBody] EstoqueUpdateViewModel model, CancellationToken ct) =>
            Ok(await _app.AtualizarAsync(id, new EstoqueCadastroDto(model.Nome, model.Tipo, model.QuantidadeMinima), ct));

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Remover(int id, CancellationToken ct)
        {
            await _app.RemoverAsync(id, ct);
            return NoContent();
        }

        /// <summary>Entrada de mercadoria (nota ou compra).</summary>
        [HttpPatch("{id:int}/reposicao")]
        public async Task<IActionResult> Repor(int id, [FromBody] EstoqueReposicaoViewModel model, CancellationToken ct) =>
            Ok(await _app.ReporAsync(id, model.Quantidade, ct));
    }
}
