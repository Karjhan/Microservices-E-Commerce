using System.Text.Json;
using Application.DependencyInjection;
using Catalog.Endpoints.Health;
using Catalog.Endpoints.Products;
using Catalog.Middleware;
using Infrastructure.Configuration;
using Infrastructure.DependencyInjection;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using OpenTelemetry.Logs;

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
    
    healthChecks.AddCheck<MinioHealthCheck>("minio");
}

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

if (!isTesting && isDevelopment)
{
    using var scope = app.Services.CreateScope();

    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    db.Database.Migrate();

    var minio = scope.ServiceProvider.GetRequiredService<IMinioClient>();
    var options = scope.ServiceProvider
        .GetRequiredService<IOptions<MinioOptions>>()
        .Value;

    var bucketExists = await minio.BucketExistsAsync(
        new BucketExistsArgs().WithBucket(options.Bucket));

    if (!bucketExists)
    {
        await minio.MakeBucketAsync(
            new MakeBucketArgs().WithBucket(options.Bucket));
    }
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

var products = app.MapGroup("/products");

products.MapCreateProduct();
products.MapDeleteProduct();
products.MapGetProductById();
products.MapGetProducts();
products.MapUpdateProduct();
products.MapUploadProductImage();
products.MapDeleteProductImage();
products.MapAddProductAttribute();
products.MapDeleteProductAttribute();
products.MapGetProductAttributes();
products.MapAddProductRelation();
products.MapDeleteProductRelation();
products.MapGetProductRelations();

app.Run();