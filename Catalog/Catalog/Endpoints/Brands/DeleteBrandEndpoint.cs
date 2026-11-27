using Application.Features.Brands.DeleteBrand;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Brands;

public static class DeleteBrandEndpoint
{
    public static IEndpointRouteBuilder MapDeleteBrand(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
            {
                // 409 in_use when products still use it.
                await mediator.Send(new DeleteBrandCommand(id), ct);

                return Results.NoContent();
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}

