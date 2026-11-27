using Application.Abstractions.Storage;
using Application.Common;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.DeleteModel;

public class DeleteProductModelHandler(
    IProductRepository repo,
    IFileStorageService storage,
    IAfterCommit afterCommit,
    ProductCacheInvalidator cache)
    : IRequestHandler<DeleteProductModelCommand>
{
    public async Task Handle(DeleteProductModelCommand request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct)
                      ?? throw new KeyNotFoundException("Product not found");

        var model = product.Model ?? throw new KeyNotFoundException("This product has no 3D model");

        product.SetModel(null);
        repo.RemoveModel(model);
        await repo.SaveChangesAsync(ct);

        var objectKey = model.ObjectKey;
        afterCommit.Register(token => storage.DeleteAsync(StorageBucket.Models, objectKey, token));

        await cache.InvalidateAsync(product.Id);
    }
}
