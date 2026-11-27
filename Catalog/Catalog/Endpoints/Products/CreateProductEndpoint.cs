using Application.Features.Products.CreateProduct;
using Domain.Commons;
using MediatR;
using Shared.Security.Authorization;

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
                request.CompatiblePrinters,
                request.BrandId,
                request.FilamentTypeIds
            );

            var productId = await mediator.Send(command);

            return Results.Created($"/products/{productId}", new { productId });
        })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

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
    PrinterType[]? CompatiblePrinters = null,

    Guid? BrandId = null,
    Guid[]? FilamentTypeIds = null
);