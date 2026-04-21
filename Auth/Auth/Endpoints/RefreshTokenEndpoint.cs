using Application.Features.RefreshToken;

namespace Auth.Endpoints;

public static class RefreshTokenEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/refresh", async (
            RefreshTokenCommand cmd,
            RefreshTokenHandler handler,
            ILogger<Program> logger) =>
        {
            logger.LogInformation("Refresh token attempt");

            var result = await handler.Handle(cmd);

            logger.LogInformation("Refresh token success");

            return Results.Ok(new
            {
                accessToken = result.accessToken,
                refreshToken = result.refreshToken
            });
        });
    }
}