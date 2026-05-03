using Application.Features.Products.CreateProduct;
using Domain.Commons;
using MediatR;

namespace Catalog.Endpoints.Products;

public static class CreateProductEndpoint
{
    public static IEndpointRouteBuilder MapCreateProduct(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", async (
            CreateProductRequest request,
            IMediator mediator) =>
        {
            var command = new CreateProductCommand(
                request.Name,
                request.ShortDescription,
                request.LongDescription,
                request.Price,
                request.CategoryId,
                request.Currency,
                request.Settings,
                request.Size,
                request.Tags,
                request.SupportedMaterials,
                request.CompatiblePrinters
            );

            var productId = await mediator.Send(command);

            return Results.Created($"/products/{productId}", new { productId });
        });

        return app;
    }
}

public record CreateProductRequest(
    string Name,
    string ShortDescription,
    string LongDescription,
    decimal Price,
    Guid CategoryId,

    string Currency,

    PrintSettings Settings,
    Dimensions Size,

    string[]? Tags = null,
    MaterialType[]? SupportedMaterials = null,
    PrinterType[]? CompatiblePrinters = null
);