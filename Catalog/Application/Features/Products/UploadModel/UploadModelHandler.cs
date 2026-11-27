using Application.Abstractions.Storage;
using Application.Common;
using Application.DTOs;
using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.UploadModel;

public class UploadProductModelHandler(
    IProductRepository repo,
    IFileStorageService storage,
    IAfterCommit afterCommit,
    ProductCacheInvalidator cache)
    : IRequestHandler<UploadProductModelCommand, UploadProductModelResult>
{
    public static string AllowedFormatsDescription =>
        "GLB, GLTF and STL (with 3D preview), or 3MF and OBJ (stored without preview), up to 100 MB";

    public async Task<UploadProductModelResult> Handle(UploadProductModelCommand request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct)
                      ?? throw new KeyNotFoundException("Product not found");

        var format = ProductModel.FormatOf(request.FileName);

        if (format is null || !ProductModel.AllowedFormats.Contains(format, StringComparer.OrdinalIgnoreCase))
            throw new UnsupportedFileTypeException(
                $"Unsupported 3D model type. Allowed: {AllowedFormatsDescription}.");

        if (request.Length <= 0)
            throw new ValidationFailedException("file", "The file is empty.");

        if (request.Length > ProductModel.MaxSizeBytes)
            throw new PayloadTooLargeException(
                $"The 3D model is too large. Allowed: {AllowedFormatsDescription}.");

        var model = new ProductModel(Guid.NewGuid(), product.Id, format, request.FileName, request.Length);
        var previous = product.Model;

        // Stream straight to storage, then write the row; undo the object if the row cannot be written.
        await storage.UploadStreamAsync(
            StorageBucket.Models, model.ObjectKey, request.Content, request.Length, ContentTypeFor(model.Format), ct);

        try
        {
            if (previous is not null)
            {
                // One model per product: remove the old row before inserting the new one.
                product.SetModel(null);
                repo.RemoveModel(previous);
                await repo.SaveChangesAsync(ct);
            }

            await repo.AddModelAsync(model, ct);
            product.SetModel(model);
            await repo.SaveChangesAsync(ct);
        }
        catch
        {
            await DeleteQuietly(model.ObjectKey);
            throw;
        }

        if (previous is not null)
        {
            // The replaced file is removed only after the change has committed.
            var oldKey = previous.ObjectKey;
            afterCommit.Register(token => storage.DeleteAsync(StorageBucket.Models, oldKey, token));
        }

        await cache.InvalidateAsync(product.Id);

        return new UploadProductModelResult(ProductModelDto.FromEntity(model), Created: previous is null);
    }

    private static string ContentTypeFor(string format) => format switch
    {
        "glb" => "model/gltf-binary",
        "gltf" => "model/gltf+json",
        "stl" => "model/stl",
        "3mf" => "model/3mf",
        _ => "model/obj"
    };

    private async Task DeleteQuietly(string objectKey)
    {
        try
        {
            await storage.DeleteAsync(StorageBucket.Models, objectKey);
        }
        catch
        {
            // The orphan sweep removes it later.
        }
    }
}
