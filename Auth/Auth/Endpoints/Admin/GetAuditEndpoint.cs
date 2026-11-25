using Application.Features.Audit;
using Shared.Security.Authorization;

namespace Auth.Endpoints.Admin;

public static class GetAuditEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/auth/admin/audit", async (
            int? page,
            int? pageSize,
            GetAuditHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.Handle(new GetAuditQuery(page, pageSize), cancellationToken);

            return Results.Ok(result);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly);
    }
}
