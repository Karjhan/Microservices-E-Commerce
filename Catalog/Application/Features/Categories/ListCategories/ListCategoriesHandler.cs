using Application.DTOs;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Categories.ListCategories;

public class ListCategoriesHandler(ICategoryRepository repo) : IRequestHandler<ListCategoriesQuery, List<CategoryDto>>
{
    public async Task<List<CategoryDto>> Handle(ListCategoriesQuery request, CancellationToken ct)
    {
        var entries = await repo.ListAsync(request.IncludeInactive, ct);

        // Usage counts help admins decide between deleting and deactivating, so shoppers do not get them.
        var counts = request.IncludeInactive
            ? await repo.CountProductsUsingAsync(entries.Select(x => x.Id).ToList(), ct)
            : null;

        return entries
            .Select(x => CategoryDto.FromEntity(x, counts?[x.Id]))
            .ToList();
    }
}
