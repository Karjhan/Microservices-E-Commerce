using Application.Abstractions.Storage;
using Application.Common;
using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.Abstractions.Persistence;
using Infrastructure.Storage;
using MediatR;

namespace Application.Features.Brands.UploadLogo;

/// <summary>Returns the public URL of the new logo.</summary>
public class UploadBrandLogoHandler(
    IBrandRepository repo,
    IFileStorageService storage,
    ProductCacheInvalidator cache,
    IAfterCommit afterCommit)
    : IRequestHandler<UploadBrandLogoCommand, string>
{
    public async Task<string> Handle(UploadBrandLogoCommand request, CancellationToken ct)
    {
        var brand = await repo.GetByIdAsync(request.BrandId, ct)
                    ?? throw new KeyNotFoundException("Brand not found");

        if (request.Length <= 0)
            throw new ValidationFailedException("file", "The file is empty.");

        if (request.Length > ProductImage.MaxSizeBytes)
            throw new PayloadTooLargeException(
                $"Image is too large. Allowed: {FileSignatureValidator.AllowedImageTypesDescription}.");

        // Same rule as product images: the bytes decide the type (FR-015).
        var contentType = await FileSignatureValidator.DetectImageContentTypeAsync(request.Content, ct);

        if (contentType is null)
            throw new UnsupportedFileTypeException(
                $"Unsupported image type. Allowed: {FileSignatureValidator.AllowedImageTypesDescription}.");

        var objectKey = Brand.BuildLogoKey(brand.Id, Guid.NewGuid());
        var previousKey = brand.LogoObjectKey;

        // Store the object first, then write the row; undo the object if the row cannot be written.
        var url = await storage.UploadStreamAsync(
            StorageBucket.Images, objectKey, request.Content, request.Length, contentType, ct);

        try
        {
            brand.SetLogo(objectKey, url);
            await repo.SaveChangesAsync(ct);
        }
        catch
        {
            await DeleteQuietly(objectKey);
            throw;
        }

        if (previousKey is not null)
            afterCommit.Register(token => storage.DeleteAsync(StorageBucket.Images, previousKey, token));

        await cache.InvalidateAllAsync();

        return url;
    }

    private async Task DeleteQuietly(string objectKey)
    {
        try
        {
            await storage.DeleteAsync(StorageBucket.Images, objectKey);
        }
        catch
        {
            // The orphan sweep removes it later.
        }
    }
}
