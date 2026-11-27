using Application.DTOs;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.GetAdminProducts;

/// <summary>
/// Admin list over every status. Not cached: admins must see their own changes immediately.
/// </summary>
public class GetAdminProductsHandler(IProductRepository repo)
    : IRequestHandler<GetAdminProductsQuery, PaginatedListDto<ProductDto>>
{
    public async Task<PaginatedListDto<ProductDto>> Handle(GetAdminProductsQuery request, CancellationToken ct)
    {
        var (items, total) = await repo.GetAdminPageAsync(request.Filter, ct);

        return new PaginatedListDto<ProductDto>
        {
            Items = items.Select(ProductDto.FromEntity).ToList(),
            Page = Math.Max(1, request.Filter.Page),
            PageSize = Math.Clamp(request.Filter.PageSize, 1, Domain.Commons.AdminProductFilter.MaxPageSize),
            TotalCount = total
        };
    }
}
