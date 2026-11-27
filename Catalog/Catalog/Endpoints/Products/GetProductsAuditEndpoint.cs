using Application.Features.Audit;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Products;

public static class GetProductsAuditEndpoint
{
    public static IEndpointRouteBuilder MapGetProductsAudit(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/audit", async (
                int? page,
                int? pageSize,
                IMediator mediator,
                CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetAuditQuery(page, pageSize), ct)))
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}