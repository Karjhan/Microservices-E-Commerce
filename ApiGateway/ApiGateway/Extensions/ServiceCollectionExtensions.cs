using ApiGateway.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Security.Configuration;

namespace ApiGateway.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayServices(this IServiceCollection services, IConfiguration config)
    {
        // CORS
        var cors = config.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
        services.AddCors(options =>
        {
            options.AddPolicy(CorsOptions.PolicyName, policy =>
            {
                if (cors.AllowedOrigins.Length > 0)
                {
                    policy.WithOrigins(cors.AllowedOrigins);
                }
                else
                {
                    policy.SetIsOriginAllowed(_ => false);
                }

                policy.AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("X-Correlation-Id");
            });
        });

        // JWT validation, the AdminOnly policy and 401/403 problem responses are shared with every service
        // so the rules cannot drift (FR-001, FR-005). The gateway refuses admin routes early; the services
        // stay authoritative because they can also be called directly.
        services.AddSharedSecurity(config);

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