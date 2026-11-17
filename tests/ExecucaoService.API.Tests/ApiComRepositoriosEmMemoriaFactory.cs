using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using ExecucaoService.Application.Interfaces;
using ExecucaoService.Testes.Compartilhado;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace ExecucaoService.API.Tests
{
    /// <summary>
    /// API com os repositórios trocados por versões em memória (as mesmas regras de reserva e
    /// baixa do DynamoDB), para testar rotas, autorização e o tratamento de erros sem banco.
    /// </summary>
    public class ApiComRepositoriosEmMemoriaFactory : ExecucaoServiceApiFactory
    {
        public EstoqueEmMemoria Estoque { get; } = new();
        public ServicosEmMemoria Servicos { get; } = new();
        public ExecucoesEmMemoria Execucoes { get; } = new();
        public RelogioFixo Relogio { get; } = new();
        public RegistroMensagensEmMemoria Mensagens { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEstoqueRepository>();
                services.RemoveAll<IServicoOficinaRepository>();
                services.RemoveAll<IExecucaoRepository>();
                services.RemoveAll<IRelogio>();
                services.RemoveAll<IRegistroMensagens>();

                services.AddSingleton<IEstoqueRepository>(Estoque);
                services.AddSingleton<IServicoOficinaRepository>(Servicos);
                services.AddSingleton<IExecucaoRepository>(Execucoes);
                services.AddSingleton<IRelogio>(Relogio);
                services.AddSingleton<IRegistroMensagens>(Mensagens);
            });
        }

        /// <summary>Cliente com um JWT igual ao emitido pelo MechanicLtda para o papel informado.</summary>
        public HttpClient CriarClienteAutenticado(string papel = "Administrador")
        {
            var cliente = CreateClient();
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GerarToken(papel));
            return cliente;
        }

        public static string GerarToken(string papel)
        {
            var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ChaveJwtTeste));
            var token = new JwtSecurityToken(
                issuer: "MechanicLtda.API",
                audience: "MechanicLtda.Clients",
                claims: [new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, papel)],
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(chave, SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
