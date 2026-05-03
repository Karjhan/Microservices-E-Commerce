using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Catalog.Endpoints.Health;

public static class HealthCheckEndpoint
{
    public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";

                var result = JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        error = e.Value.Exception?.Message
                    }),
                    totalDuration = report.TotalDuration
                });

                await context.Response.WriteAsync(result);
            }
        });

        return app;
    }
}