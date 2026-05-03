using Application.Features.Products.DeleteAttribute;
using MediatR;

namespace Catalog.Endpoints.Products;

public static class DeleteProductAttributeEndpoint
{
    public static IEndpointRouteBuilder MapDeleteProductAttribute(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{productId:guid}/attributes/{attributeId:guid}", async (
            Guid productId,
            Guid attributeId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new DeleteProductAttributeCommand(productId, attributeId), ct);
            return Results.NoContent();
        });

        return app;
    }
}