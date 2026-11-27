using Application.DTOs;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Brands.ListBrands;

public class ListBrandsHandler(IBrandRepository repo) : IRequestHandler<ListBrandsQuery, List<BrandDto>>
{
    public async Task<List<BrandDto>> Handle(ListBrandsQuery request, CancellationToken ct)
    {
        var entries = await repo.ListAsync(request.IncludeInactive, ct);

        // Usage counts help admins decide between deleting and deactivating, so shoppers do not get them.
        var counts = request.IncludeInactive
            ? await repo.CountProductsUsingAsync(entries.Select(x => x.Id).ToList(), ct)
            : null;

        return entries
            .Select(x => BrandDto.FromEntity(x, counts?[x.Id]))
            .ToList();
    }
}
