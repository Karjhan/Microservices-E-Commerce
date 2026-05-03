using Application.Features.Products.UpdateProduct;
using Domain.Commons;
using MediatR;

namespace Catalog.Endpoints.Products;

public static class UpdateProductEndpoint
{
    public static IEndpointRouteBuilder MapUpdateProduct(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:guid}", async (
            Guid id,
            UpdateProductRequest request,
            IMediator mediator) =>
        {
            var command = new UpdateProductCommand(
                id,
                request.Name,
                request.ShortDescription,
                request.LongDescription,
                request.Price,
                request.Currency,
                request.Settings,
                request.Size,
                request.Tags,
                request.SupportedMaterials,
                request.CompatiblePrinters,
                request.DownloadCount,
                request.AverageRating
            );

            await mediator.Send(command);

            return Results.NoContent();
        });

        return app;
    }
}

public record UpdateProductRequest(
    string Name,
    string ShortDescription,
    string LongDescription,
    decimal Price,

    string Currency,

    PrintSettings Settings,
    Dimensions Size,

    string[]? Tags = null,
    MaterialType[]? SupportedMaterials = null,
    PrinterType[]? CompatiblePrinters = null,
    
    int DownloadCount = 0,
    double AverageRating = 0
);