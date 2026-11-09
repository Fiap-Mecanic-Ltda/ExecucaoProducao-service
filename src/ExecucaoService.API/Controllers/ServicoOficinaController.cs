using ExecucaoService.API.ViewModels;
using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.DTOs;
using ExecucaoService.Bootstrap.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExecucaoService.API.Controllers
{
    /// <summary>Catálogo de serviços da oficina. Mesmas rotas do MechanicLtda. [Admin]</summary>
    [Route("api/servicos-oficina")]
    [Authorize(Roles = Roles.Admin)]
    public class ServicoOficinaController : BaseApiController
    {
        private readonly IServicoOficinaAppService _app;

        public ServicoOficinaController(IServicoOficinaAppService app) => _app = app;

        [HttpGet]
        public async Task<IActionResult> Listar(CancellationToken ct) => Ok(await _app.ListarAsync(false, ct));

        [HttpGet("ativos")]
        public async Task<IActionResult> ListarAtivos(CancellationToken ct) => Ok(await _app.ListarAsync(true, ct));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Obter(int id, CancellationToken ct) => Ok(await _app.ObterAsync(id, ct));

        [HttpPost]
        public async Task<IActionResult> Criar([FromBody] ServicoOficinaViewModel model, CancellationToken ct)
        {
            var criado = await _app.AdicionarAsync(Dto(model), ct);
            return CreatedAtAction(nameof(Obter), new { id = criado.Id }, criado);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Atualizar(int id, [FromBody] ServicoOficinaViewModel model, CancellationToken ct) =>
            Ok(await _app.AtualizarAsync(id, Dto(model), ct));

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Remover(int id, CancellationToken ct)
        {
            await _app.RemoverAsync(id, ct);
            return NoContent();
        }

        private static ServicoOficinaInputDto Dto(ServicoOficinaViewModel m) => new(m.Nome, m.Descricao, m.ValorBase, m.Ativo);
    }
}
