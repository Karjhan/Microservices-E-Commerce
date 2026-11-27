using Application.Common;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Categories.UpdateCategory;

public class UpdateCategoryHandler(ICategoryRepository repo, ProductCacheInvalidator cache)
    : IRequestHandler<UpdateCategoryCommand>
{
    public async Task Handle(UpdateCategoryCommand request, CancellationToken ct)
    {
        var entry = await repo.GetByIdAsync(request.Id, ct)
                    ?? throw new KeyNotFoundException("Category not found");

        var name = ReferenceRules.CleanName(request.Name);
        await ReferenceRules.EnsureNameAvailableAsync(repo, name, "category", entry.Id, ct);

        entry.Rename(name);

        await repo.SaveChangesAsync(ct);

        // Cached product details carry this name.
        await cache.InvalidateAllAsync();
    }
}
