using Microsoft.OpenApi.Models;

namespace ExecucaoService.API.Extensions
{
    public static class SwaggerExtension
    {
        public static IServiceCollection AddSwaggerWithAuth(this IServiceCollection services)
        {
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title       = "Execucao Service API",
                    Version     = "v1",
                    Description = "Execução e Produção da MechanicLtda: fila de execução, diagnóstico, estoque e catálogo de serviços."
                });

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name         = "Authorization",
                    Type         = SecuritySchemeType.Http,
                    Scheme       = "Bearer",
                    BearerFormat = "JWT",
                    In           = ParameterLocation.Header,
                    Description  = "Informe o token JWT emitido pelo login (MechanicLtda) ou pela Lambda de CPF."
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            return services;
        }
    }
}
