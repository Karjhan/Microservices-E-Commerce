using Application.Features.Products.SetPrimaryImage;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Products;

public static class SetPrimaryImageEndpoint
{
    public static IEndpointRouteBuilder MapSetPrimaryImage(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/images/{imageId:guid}/primary", async (
                Guid id,
                Guid imageId,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var images = await mediator.Send(new SetPrimaryImageCommand(id, imageId), ct);
                return Results.Ok(images);
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}
