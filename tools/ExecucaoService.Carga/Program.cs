using ExecucaoService.Application.AppServices;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Bootstrap.Extensions;
using Amazon.DynamoDBv2.Model;
using ExecucaoService.Infrastructure.Integracoes;
using ExecucaoService.Infrastructure.Persistencia;
using ExecucaoService.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Carga inicial do estoque e do catálogo de serviços: lê do MechanicLtda e grava nas tabelas da
// Execução com os mesmos ids. Pode rodar de novo sem duplicar: ids já carregados ficam como estão.
//
//   MONOLITO_URL=http://localhost:8080 MONOLITO_TOKEN=<jwt> \
//   DynamoDb__ServiceUrl=http://localhost:8000 AWS_ACCESS_KEY_ID=local AWS_SECRET_ACCESS_KEY=local \
//     dotnet run --project tools/ExecucaoService.Carga
//
// Na AWS, sem DynamoDb__ServiceUrl, valem DynamoDb__Regiao, DynamoDb__PrefixoTabelas e as
// credenciais padrão do SDK.

var configuracao = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var urlMonolito = configuracao["MONOLITO_URL"];
var token       = configuracao["MONOLITO_TOKEN"];
if (string.IsNullOrWhiteSpace(urlMonolito) || string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Informe MONOLITO_URL (ex.: http://localhost:8080) e MONOLITO_TOKEN (JWT de Administrador ou Funcionário).");
    return 2;
}

var services = new ServiceCollection();
services.AddLogging(l => l.AddSimpleConsole());
services.AddDatabase(configuracao);
services.AddScoped<IEstoqueRepository, EstoqueRepository>();
services.AddScoped<IServicoOficinaRepository, ServicoOficinaRepository>();
services.AddScoped<ICargaCatalogoAppService, CargaCatalogoAppService>();
services.AddSingleton<InicializadorDynamoDb>();

await using var provider = services.BuildServiceProvider();

// Com DynamoDb__CriarTabelas=true (DynamoDB Local), cria as tabelas que faltam, como a API faz.
await provider.GetRequiredService<InicializadorDynamoDb>().StartAsync(CancellationToken.None);
using var http = new HttpClient { BaseAddress = new Uri(urlMonolito.TrimEnd('/') + "/") };
var monolito = new CatalogoMonolitoClient(http, token);

var estoque  = await monolito.ObterEstoqueAsync();
var servicos = await monolito.ObterServicosAsync();
Console.WriteLine($"MechanicLtda: {estoque.Count} item(ns) de estoque e {servicos.Count} serviço(s).");

using var escopo = provider.CreateScope();
ExecucaoService.Application.DTOs.ResultadoCargaDto resultado;
try
{
    resultado = await escopo.ServiceProvider.GetRequiredService<ICargaCatalogoAppService>()
                            .CarregarAsync(estoque.ToList(), servicos.ToList());
}
catch (ResourceNotFoundException)
{
    Console.Error.WriteLine("As tabelas da Execução não existem com esse prefixo. Na AWS, aplique o ExecucaoProducao-InfraSGBD; " +
                            "no DynamoDB Local, suba a API uma vez ou rode com DynamoDb__CriarTabelas=true.");
    return 2;
}

Console.WriteLine($"Estoque: {resultado.EstoqueCriados} criado(s), {resultado.EstoqueJaExistentes} já existia(m).");
Console.WriteLine($"Serviços: {resultado.ServicosCriados} criado(s), {resultado.ServicosJaExistentes} já existia(m).");
foreach (var rejeitado in resultado.Rejeitados)
    Console.Error.WriteLine($"Rejeitado: {rejeitado}");

return resultado.Rejeitados.Count == 0 ? 0 : 1;
