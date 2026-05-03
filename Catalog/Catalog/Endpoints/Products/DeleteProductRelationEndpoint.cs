using Application.Features.Products.DeleteRelation;
using MediatR;

namespace Catalog.Endpoints.Products;

public static class DeleteProductRelationEndpoint
{
    public static IEndpointRouteBuilder MapDeleteProductRelation(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{productId:guid}/relations/{relationId:guid}", async (
            Guid productId,
            Guid relationId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new DeleteProductRelationCommand(productId, relationId), ct);
            return Results.NoContent();
        });

        return app;
    }
}