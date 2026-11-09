using Microsoft.AspNetCore.HttpOverrides;
using ExecucaoService.API.Extensions;
using ExecucaoService.API.Middlewares;
using ExecucaoService.Bootstrap.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Logs em JSON (com o CorrelationId nos escopos) fora do ambiente de desenvolvimento.
builder.AddLogsEstruturados();

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

builder.Services.AddControllers();

// Rotas em minúsculas: o roteamento do API Gateway diferencia maiúsculas, então Swagger,
// Postman e as rotas do gateway precisam usar exatamente a mesma grafia.
builder.Services.AddRouting(options => options.LowercaseUrls = true);

// O tráfego chega pelo API Gateway e pelo ALB interno; sem confiar nos headers de
// encaminhamento, o ASP.NET veria o IP e o esquema do proxy, não os do cliente.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                               | ForwardedHeaders.XForwardedProto
                               | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithAuth();

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseCorrelationId();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Liveness usado pelos probes do Kubernetes, pelo health check do ALB e pelo docker-compose.
app.MapGet("/health", () => Results.Ok(new { status = "ok", servico = "execucao-service" }))
   .AllowAnonymous()
   .ExcludeFromDescription();

app.Run();

// Expõe a classe Program para o WebApplicationFactory dos testes de API.
public partial class Program { }
