using ApiGateway.Extensions;
using ApiGateway.Middleware;
using Infrastructure.DependencyInjection;
using OpenTelemetry.Logs;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console();
});

// Services
builder.Services.AddGatewayServices(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;
    options.ParseStateValues = true;

    options.AddOtlpExporter(opt =>
    {
        opt.Endpoint = new Uri(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? "http://jaeger:4317");
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// YARP
app.MapReverseProxy();

// Endpoints
app.MapHealthEndpoints();

app.Run();