using Application.Features.Products.DeleteImage;
using MediatR;

namespace Catalog.Endpoints.Products;

public static class DeleteProductImageEndpoint
{
    public static IEndpointRouteBuilder MapDeleteProductImage(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{productId:guid}/images/{imageId:guid}", async (
            Guid productId,
            Guid imageId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new DeleteProductImageCommand(productId, imageId), ct);
            return Results.NoContent();
        });

        return app;
    }
}