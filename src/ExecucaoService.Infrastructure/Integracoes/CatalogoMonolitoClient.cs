using ExecucaoService.Application.DTOs;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ExecucaoService.Infrastructure.Integracoes
{
    /// <summary>
    /// Lê o estoque e o catálogo de serviços da API do MechanicLtda, com o token de um
    /// administrador ou funcionário. Usado só pela carga inicial (tools/ExecucaoService.Carga).
    /// </summary>
    public class CatalogoMonolitoClient
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;

        public CatalogoMonolitoClient(HttpClient http, string token)
        {
            _http = http;
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public Task<IReadOnlyList<EstoqueOrigemDto>> ObterEstoqueAsync(CancellationToken ct = default) =>
            ObterListaAsync<EstoqueOrigemDto>("api/estoque", ct);

        public Task<IReadOnlyList<ServicoOficinaOrigemDto>> ObterServicosAsync(CancellationToken ct = default) =>
            ObterListaAsync<ServicoOficinaOrigemDto>("api/servicos-oficina", ct);

        private async Task<IReadOnlyList<T>> ObterListaAsync<T>(string rota, CancellationToken ct)
        {
            using var resposta = await _http.GetAsync(rota, ct);
            if (!resposta.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"GET {rota} no MechanicLtda respondeu {(int)resposta.StatusCode}. Confira a URL e se o token é de Administrador ou Funcionário.");

            return await resposta.Content.ReadFromJsonAsync<List<T>>(Json, ct) ?? [];
        }
    }
}
