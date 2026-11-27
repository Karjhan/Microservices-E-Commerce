using Application.Features.Products.GetAdminProducts;
using Domain.Commons;
using Domain.Enums;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Products;

public static class GetAdminProductsEndpoint
{
    public static IEndpointRouteBuilder MapGetAdminProducts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin", async (
                string? q,
                ProductStatus? status,
                Guid? categoryId,
                Guid[]? productIds,
                string? sortBy,
                string? sortDir,
                int? page,
                int? pageSize,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var filter = new AdminProductFilter
                {
                    Search = q,
                    Status = status,
                    CategoryId = categoryId,
                    ProductIds = productIds is { Length: > 0 } ? productIds : null,
                    SortBy = ParseSort(sortBy),
                    Descending = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase),
                    Page = page is > 0 ? page.Value : 1,
                    PageSize = pageSize is > 0 ? pageSize.Value : 25
                };

                var result = await mediator.Send(new GetAdminProductsQuery(filter), ct);
                return Results.Ok(result);
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }

    private static AdminProductSort ParseSort(string? sortBy) =>
        Enum.TryParse<AdminProductSort>(sortBy, ignoreCase: true, out var sort) ? sort : AdminProductSort.CreatedAt;
}
