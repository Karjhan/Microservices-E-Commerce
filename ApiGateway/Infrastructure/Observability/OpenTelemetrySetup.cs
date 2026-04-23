using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Infrastructure.Observability;

public static class OpenTelemetrySetup
{
    public static IServiceCollection AddOpenTelemetry(this IServiceCollection services, IConfiguration config)
    {
        services.AddOpenTelemetry()
            .WithTracing(b =>
            {
                b.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("gateway"))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(config["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://jaeger:4317");
                    });
            });

        return services;
    }
}