using System.Text;
using ApiGateway.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayServices(this IServiceCollection services, IConfiguration config)
    {
        // JWT
        var jwt = config.GetSection("Jwt").Get<JwtOptions>()!;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt.Secret))
                };
            });

        services.AddAuthorization();

        // Rate Limiting
        services.AddRateLimiter(opt =>
        {
            opt.AddFixedWindowLimiter("auth-strict", o =>
            {
                o.PermitLimit = 10;
                o.Window = TimeSpan.FromSeconds(60);
            });

            opt.AddFixedWindowLimiter("global", o =>
            {
                o.PermitLimit = 100;
                o.Window = TimeSpan.FromSeconds(60);
            });
        });

        // YARP
        services.AddReverseProxy()
            .LoadFromConfig(config.GetSection("ReverseProxy"))
            .AddTransforms<Infrastructure.ReverseProxy.TransformProviders.AddCorrelationIdTransform>()
            .AddTransforms<Infrastructure.ReverseProxy.TransformProviders.AddUserClaimsTransform>();

        return services;
    }
}