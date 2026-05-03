using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Infrastructure.Observability;

public static class OpenTelemetrySetup
{
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration config)
    {
        var serviceName = config["ServiceName"] ?? "catalog-service";

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(
                        ResourceBuilder.CreateDefault()
                            .AddService(serviceName))

                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })

                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })

                    .AddEntityFrameworkCoreInstrumentation()
                    
                    .AddRedisInstrumentation()

                    .AddOtlpExporter(opt =>
                    {
                        opt.Endpoint = new Uri(
                            config["OTEL_EXPORTER_OTLP_ENDPOINT"] ??
                            "http://jaeger:4317");
                    });
            });

        return services;
    }
}