using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace AgtcSrvIngestion.Infrastructure.Configuration;

[ExcludeFromCodeCoverage]
public static class JwtBearerConfiguration
{
    public static void ConfigureJwtBearer(this IServiceCollection services, IConfiguration configuration, string jwtSigningKey)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "AgroTechAuth";
        var audience = configuration["Jwt:Audience"] ?? "AgroTechAuth";
        var keyBytes = Convert.FromBase64String(jwtSigningKey);

        services
            .AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {

                options.RequireHttpsMetadata = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true, 
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = true, 
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero 
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";

                        var traceId = context.HttpContext.Items["X-Correlation-ID"]?.ToString() ?? context.HttpContext.TraceIdentifier;
                        var response = new
                        {
                            error = "Unauthorized",
                            statusCode = 401,
                            message = "Token ausente, inválido ou expirado.",
                            traceId
                        };

                        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });

                        await context.Response.WriteAsync(json);
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";

                        var traceId = context.HttpContext.Items["X-Correlation-ID"]?.ToString() ?? context.HttpContext.TraceIdentifier;
                        var response = new
                        {
                            error = "Forbidden",
                            statusCode = 403,
                            message = "Você não tem permissão para acessar este recurso.",
                            traceId
                        };

                        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });

                        await context.Response.WriteAsync(json);
                    }
                };
            });
    }
}
