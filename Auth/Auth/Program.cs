using System.Text;
using System.Text.Json;
using Application.Features.GoogleLogin;
using Application.Features.Login;
using Application.Features.RefreshToken;
using Application.Features.Register;
using Auth.Extensions;
using Auth.Middleware;
using Domain.Abstractions.Authentication;
using Domain.Abstractions.Messaging;
using Domain.Abstractions.Persistence;
using Infrastructure;
using Infrastructure.Authentication;
using Infrastructure.External.Google;
using Infrastructure.Messaging;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var isTesting = builder.Environment.IsEnvironment("Testing");
var isDevelopment = builder.Environment.IsEnvironment("Development");

if (isTesting)
{
    builder.Services.AddSingleton<IEventPublisher, NoOpEventPublisher>();
}
else
{
    builder.Services.AddSingleton<IEventPublisher, RabbitMqPublisher>();
}

builder.Services.AddDbContext<AuthDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
});

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IJwtProvider, JwtProvider>();

builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<RefreshTokenHandler>();

builder.Services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
builder.Services.AddScoped<GoogleLoginHandler>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService("AuthService"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(opt =>
            {
                opt.Endpoint = new Uri(builder.Configuration.GetValue<string>("OtelExportUri") ?? string.Empty);
            });
    });

if (!isTesting)
{
    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("Default")!)
        .AddRabbitMQ(async sp =>
        {
            var config = builder.Configuration.GetSection("RabbitMq");

            var factory = new RabbitMQ.Client.ConnectionFactory
            {
                HostName = config["Host"],
                Port = int.Parse(config["Port"]!),
                UserName = config["Username"],
                Password = config["Password"]
            };

            return await factory.CreateConnectionAsync();
        });
}

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;
    options.ParseStateValues = true;

    options.AddOtlpExporter(opt =>
    {
        opt.Endpoint = new Uri(
            builder.Configuration["OtelExportUri"]
            ?? "http://jaeger:4317");
    });
});
var app = builder.Build();

app.UseGlobalExceptionHandling();

app.Use(async (HttpContext context, RequestDelegate next) =>
{
    context.TraceIdentifier = Guid.NewGuid().ToString();
    await next(context);
});

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

if (!isTesting && isDevelopment)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
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
app.MapAuthEndpoints();

app.Run();