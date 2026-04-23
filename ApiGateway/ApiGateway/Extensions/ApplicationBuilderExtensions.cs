namespace ApiGateway.Extensions;

public static class ApplicationBuilderExtensions
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok("Gateway OK"));
    }
}