using Application.Features.Products.SetStatus;
using Domain.Enums;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Products;

public static class SetProductStatusEndpoint
{
    public static IEndpointRouteBuilder MapSetProductStatus(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/status", async (
                Guid id,
                SetProductStatusRequest request,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var result = await mediator.Send(new SetProductStatusCommand(id, request.Status), ct);
                return Results.Ok(result);
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}

public record SetProductStatusRequest(ProductStatus Status);
