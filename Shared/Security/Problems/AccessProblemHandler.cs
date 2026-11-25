using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shared.Security.Audit;

namespace Shared.Security.Problems;

public sealed class AccessProblemHandler : IAuthorizationMiddlewareResultHandler
{
    public const string UnauthenticatedCode = "unauthenticated";
    public const string ForbiddenCode = "forbidden";

    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var challenged = authorizeResult.Challenged || context.User.Identity?.IsAuthenticated != true;

        var statusCode = challenged ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden;
        var code = challenged ? UnauthenticatedCode : ForbiddenCode;

        var recorder = context.RequestServices.GetService<IRefusalAuditRecorder>();
        if (recorder is not null)
        {
            await recorder.RecordAsync(context, code, context.RequestAborted);
        }

        context.Response.StatusCode = statusCode;
        if (challenged)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.example/{statusCode}",
            Title = challenged ? "Unauthorized" : "Forbidden",
            Status = statusCode,
            Detail = challenged
                ? "Authentication is required to access this resource."
                : "You do not have permission to perform this action.",
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;

        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }
}
