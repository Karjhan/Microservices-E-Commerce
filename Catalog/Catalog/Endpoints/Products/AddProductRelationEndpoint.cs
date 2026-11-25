using Application.Features.Products.AddRelation;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Products;

public static class AddProductRelationEndpoint
{
    public static IEndpointRouteBuilder MapAddProductRelation(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{productId:guid}/relations", async (
            Guid productId,
            AddProductRelationRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var relationId = await mediator.Send(
                new AddProductRelationCommand(productId, request.RelatedProductId, request.Type),
                ct);

            return Results.Created($"/products/{productId}/relations/{relationId}", new
            {
                relationId
            });
        })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}

public record AddProductRelationRequest(
    Guid RelatedProductId,
    string Type
);