using Infrastructure.Observability;
using Infrastructure.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddOpenTelemetry(config);

        services.AddHttpClient("proxy")
            .AddPolicyHandler(PollyPolicies.GetRetryPolicy())
            .AddPolicyHandler(PollyPolicies.GetCircuitBreakerPolicy());

        return services;
    }
}