using ApiGateway.Extensions;
using ApiGateway.Middleware;
using Infrastructure.DependencyInjection;
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

var app = builder.Build();

// Middleware order is CRITICAL
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