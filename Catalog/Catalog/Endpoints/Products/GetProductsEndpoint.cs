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
            int page,
            int pageSize,
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
                Page = page == 0 ? 1 : page,
                PageSize = pageSize == 0 ? 20 : pageSize
            };

            var result = await mediator.Send(new GetProductsQuery(filter));
            return Results.Ok(result);
        });

        return app;
    }
}