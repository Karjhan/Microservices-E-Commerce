using Application.Features.Products.AddAttribute;
using MediatR;

namespace Catalog.Endpoints.Products;

public static class AddProductAttributeEndpoint
{
    public static IEndpointRouteBuilder MapAddProductAttribute(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{productId:guid}/attributes", async (
            Guid productId,
            AddProductAttributeRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var attributeId = await mediator.Send(
                new AddProductAttributeCommand(productId, request.Key, request.Value),
                ct);

            return Results.Created($"/products/{productId}/attributes/{attributeId}", new
            {
                attributeId
            });
        });

        return app;
    }
}

public record AddProductAttributeRequest(
    string Key,
    string Value
);