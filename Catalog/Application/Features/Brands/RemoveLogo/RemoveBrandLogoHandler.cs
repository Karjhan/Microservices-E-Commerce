using Application.Abstractions.Storage;
using Application.Common;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Brands.RemoveLogo;

public class RemoveBrandLogoHandler(
    IBrandRepository repo,
    IFileStorageService storage,
    ProductCacheInvalidator cache,
    IAfterCommit afterCommit)
    : IRequestHandler<RemoveBrandLogoCommand>
{
    public async Task Handle(RemoveBrandLogoCommand request, CancellationToken ct)
    {
        var brand = await repo.GetByIdAsync(request.BrandId, ct)
                    ?? throw new KeyNotFoundException("Brand not found");

        var key = brand.LogoObjectKey;

        if (key is null)
            return;

        brand.ClearLogo();
        await repo.SaveChangesAsync(ct);

        afterCommit.Register(token => storage.DeleteAsync(StorageBucket.Images, key, token));

        await cache.InvalidateAllAsync();
    }
}
