using Application.Features.Products.GetProducts;
using Domain.Commons;
using Domain.Enums;
using MediatR;

namespace Catalog.Endpoints.Products;

public static class GetProductsEndpoint
{
    public static IEndpointRouteBuilder MapGetProducts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", async (
            Guid? categoryId,
            decimal? minPrice,
            decimal? maxPrice,
            string[]? tags,
            MaterialType? material,
            PrinterType? printerType,
            string? search,
            ProductStatus? status,
            int? page,
            int? pageSize,
            IMediator mediator) =>
        {
            var filter = new ProductFilter
            {
                CategoryId = categoryId,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                Tags = tags != null ? tags.ToList() : new List<string>(),
                Material = material,
                PrinterType = printerType,
                Search = search,
                Status = status,
                Page = page is > 0 ? page.Value : 1,
                PageSize = pageSize is > 0 ? pageSize.Value : 20
            };

            var result = await mediator.Send(new GetProductsQuery(filter));
            return Results.Ok(result);
        });

        return app;
    }
}