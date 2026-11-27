using Application.Common;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Brands.UpdateBrand;

public class UpdateBrandHandler(IBrandRepository repo, ProductCacheInvalidator cache)
    : IRequestHandler<UpdateBrandCommand>
{
    public async Task Handle(UpdateBrandCommand request, CancellationToken ct)
    {
        var entry = await repo.GetByIdAsync(request.Id, ct)
                    ?? throw new KeyNotFoundException("Brand not found");

        var name = ReferenceRules.CleanName(request.Name);
        await ReferenceRules.EnsureNameAvailableAsync(repo, name, "brand", entry.Id, ct);

        entry.Rename(name);

        await repo.SaveChangesAsync(ct);

        // Cached product details carry this name.
        await cache.InvalidateAllAsync();
    }
}
