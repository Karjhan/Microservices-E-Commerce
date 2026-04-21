using Application.Features.GoogleLogin;

namespace Auth.Endpoints;

public static class GoogleLoginEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/google", async (
            GoogleLoginCommand cmd,
            GoogleLoginHandler handler,
            ILogger<Program> logger) =>
        {
            logger.LogInformation("Google login attempt");

            var result = await handler.Handle(cmd);

            logger.LogInformation("Google login success");

            return Results.Ok(new
            {
                accessToken = result.accessToken,
                refreshToken = result.refreshToken
            });
        });
    }
}