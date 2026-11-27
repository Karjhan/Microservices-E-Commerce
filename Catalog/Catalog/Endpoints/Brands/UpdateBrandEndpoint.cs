using Application.Features.Brands.UpdateBrand;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Brands;

public static class UpdateBrandEndpoint
{
    public static IEndpointRouteBuilder MapUpdateBrand(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:guid}", async (Guid id, UpdateBrandRequest request, IMediator mediator, CancellationToken ct) =>
            {
                await mediator.Send(new UpdateBrandCommand(id, request.Name), ct);

                return Results.NoContent();
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}

public record UpdateBrandRequest(string Name);
