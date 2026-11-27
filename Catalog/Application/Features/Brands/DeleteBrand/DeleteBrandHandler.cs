using Application.Abstractions.Storage;
using Application.Common;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Brands.DeleteBrand;

public class DeleteBrandHandler(IBrandRepository repo, IFileStorageService storage, IAfterCommit afterCommit)
    : IRequestHandler<DeleteBrandCommand>
{
    public async Task Handle(DeleteBrandCommand request, CancellationToken ct)
    {
        var brand = await repo.GetByIdAsync(request.Id, ct)
                    ?? throw new KeyNotFoundException("Brand not found");

        // 409 in_use with the product count when anything still refers to it (FR-023).
        await ReferenceRules.EnsureNotInUseAsync(repo, brand, "brand", ct);

        var logoKey = brand.LogoObjectKey;

        repo.Remove(brand);
        await repo.SaveChangesAsync(ct);

        if (logoKey is not null)
            afterCommit.Register(token => storage.DeleteAsync(StorageBucket.Images, logoKey, token));
    }
}
