using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Security.Configuration;

public static class SharedJwtBearerExtensions
{
    /// <summary>
    /// Validates issuer, audience, lifetime and signing key using the <c>Jwt</c> section.
    /// Used by every service and the gateway so the rules cannot drift (FR-001, FR-005).
    /// </summary>
    public static IServiceCollection AddSharedJwtBearer(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(SharedJwtOptions.SectionName).Get<SharedJwtOptions>()
                      ?? new SharedJwtOptions();

        if (string.IsNullOrWhiteSpace(options.Key))
        {
            throw new InvalidOperationException("Jwt:Key must be configured.");
        }

        services.Configure<SharedJwtOptions>(configuration.GetSection(SharedJwtOptions.SectionName));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = options.Issuer,
                    ValidAudience = options.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = ClaimTypes.NameIdentifier
                };
            });

        return services;
    }
}
