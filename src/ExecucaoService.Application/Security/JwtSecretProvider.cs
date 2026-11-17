using Microsoft.Extensions.Configuration;

namespace ExecucaoService.Application.Security
{
    /// <summary>
    /// Lê a chave simétrica do JWT. É a mesma chave do MechanicLtda e da Lambda de
    /// autenticação: o token emitido por eles vale aqui sem nenhuma troca.
    /// </summary>
    public static class JwtSecretProvider
    {
        public static string GetSecretKey(IConfiguration configuration)
        {
            return Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                   ?? configuration["JWT_SECRET_KEY"]
                   ?? configuration["JwtSettings:SecretKey"]
                   ?? throw new InvalidOperationException(
                       "A chave JWT não está configurada. Defina 'JWT_SECRET_KEY' como variável de ambiente ou em configuração.");
        }
    }
}
