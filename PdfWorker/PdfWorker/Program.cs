using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DependencyInjection;
using Infrastructure.Configuration;
using Infrastructure.DependencyInjection;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using PdfWorker.Endpoints;
using PdfWorker.Middleware;

var builder = WebApplication.CreateBuilder(args);

var isDevelopment = builder.Environment.IsDevelopment();
var isTesting = builder.Environment.IsEnvironment("Testing");

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
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

builder.Services.AddEndpointsApiExplorer();
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

if (!isTesting)
{
    var config = builder.Configuration;
    var healthChecks = builder.Services.AddHealthChecks();

    healthChecks.AddNpgSql(config["Database:ConnectionString"]!, name: "postgres");

    healthChecks.AddRabbitMQ(async sp =>
    {
        var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        var factory = new RabbitMQ.Client.ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.Username,
            Password = options.Password,
            VirtualHost = options.VirtualHost
        };

        return await factory.CreateConnectionAsync();
    }, name: "rabbitmq");

    healthChecks.AddCheck<MinioHealthCheck>("minio");
}

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

if (!isTesting && isDevelopment)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PdfWorkerDbContext>();
    db.Database.Migrate();
}

if (!isTesting)
{
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";

            var result = JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.ToDictionary(
                    e => e.Key,
                    e => e.Value.Status.ToString())
            });

            await context.Response.WriteAsync(result);
        }
    });
}

var documents = app.MapGroup("/documents");
documents.MapListDocuments();
documents.MapGetDocumentById();

app.Run();

public partial class Program { }
