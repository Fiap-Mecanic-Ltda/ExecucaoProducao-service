using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ExecucaoService.Application.Security;
using System.Text;

namespace ExecucaoService.API.Extensions
{
    public static class AuthenticationExtension
    {
        /// <summary>
        /// Valida o mesmo JWT do resto do sistema: mesma chave simétrica, mesma audiência e os
        /// dois emissores (login por e-mail/senha no MechanicLtda e Lambda de CPF). O serviço
        /// não emite tokens.
        /// </summary>
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var jwtSettings = configuration.GetSection("JwtSettings");
                var secretKey   = Encoding.UTF8.GetBytes(JwtSecretProvider.GetSecretKey(configuration));

                var issuers = new[] { jwtSettings["Issuer"], jwtSettings["IssuerCpf"] }
                    .Where(issuer => !string.IsNullOrWhiteSpace(issuer))
                    .ToArray();

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuers             = issuers,
                    ValidAudience            = jwtSettings["Audience"],
                    IssuerSigningKey         = new SymmetricSecurityKey(secretKey),
                    ClockSkew                = TimeSpan.Zero
                };
            });

            return services;
        }
    }
}
