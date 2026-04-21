using Application.Features.Register;

namespace Auth.Endpoints;

public static class RegisterEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/register", async (
            RegisterCommand cmd,
            RegisterHandler handler,
            ILogger<Program> logger) =>
        {
            logger.LogInformation("Register attempt for {Email}", cmd.Email);

            var id = await handler.Handle(cmd);

            logger.LogInformation("User registered with Id {UserId}", id);

            return Results.Ok(id);
        });
    }
}