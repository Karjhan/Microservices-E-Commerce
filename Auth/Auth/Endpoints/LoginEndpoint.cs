using Application.Features.Login;

namespace Auth.Endpoints;

public static class LoginEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/login", async (
            LoginCommand cmd,
            LoginHandler handler,
            ILogger<Program> logger) =>
        {
            logger.LogInformation("Login attempt for {Email}", cmd.Email);

            var (access, refresh) = await handler.Handle(cmd);

            logger.LogInformation("Login success for {Email}", cmd.Email);

            return Results.Ok(new
            {
                accessToken = access,
                refreshToken = refresh
            });
        });
    }
}