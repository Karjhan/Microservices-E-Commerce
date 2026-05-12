using System.Text.Json;
using Application.DependencyInjection;
using Infrastructure.Configuration;
using Infrastructure.DependencyInjection;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using Payments.Endpoints;
using Payments.Middleware;

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
    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});

if (!isTesting)
{
    var config = builder.Configuration;

    var healthChecks = builder.Services.AddHealthChecks();

    healthChecks.AddNpgSql(
        config["Database:ConnectionString"]!,
        name: "postgres");

    var redisConnection = config["Redis:ConnectionString"];
    if (!string.IsNullOrEmpty(redisConnection))
    {
        healthChecks.AddRedis(redisConnection, name: "redis");
    }

    healthChecks.AddRabbitMQ(async sp =>
    {
        var options = sp
            .GetRequiredService<IOptions<RabbitMqOptions>>()
            .Value;

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
}

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

if (!isTesting && isDevelopment)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
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
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    error = e.Value.Exception?.Message
                })
            });

            await context.Response.WriteAsync(result);
        }
    });
}

var payments = app.MapGroup("/payments");

payments.MapCreatePayment();
payments.MapProcessPayment();
payments.MapRefundPayment();
payments.MapGetPaymentById();
payments.MapGetPayments();
payments.MapGetPaymentByOrderId();

app.Run();

public partial class Program { }
