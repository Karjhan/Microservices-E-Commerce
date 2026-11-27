using Application.Features.Products.UpdateProduct;
using Domain.Commons;
using MediatR;
using Shared.Security.Authorization;

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
                request.CompatiblePrinters,
                request.CategoryId,
                request.BrandId,
                request.FilamentTypeIds
            );

            await mediator.Send(command);

            return Results.NoContent();
        })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

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
    PrinterType[]? CompatiblePrinters = null,

    // Null keeps the current category.
    Guid? CategoryId = null,

    // Sent as is: null removes the brand.
    Guid? BrandId = null,

    // Null keeps the current filament types; a list replaces them.
    Guid[]? FilamentTypeIds = null
);