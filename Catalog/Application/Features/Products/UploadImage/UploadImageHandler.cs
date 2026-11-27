using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Common;
using Application.DTOs;
using Contracts.Events;
using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.Abstractions.Persistence;
using Infrastructure.Storage;
using MediatR;

namespace Application.Features.Products.UploadImage;

public class UploadProductImageHandler(
    IProductRepository repo,
    IFileStorageService storage,
    IEventPublisher publisher,
    ProductCacheInvalidator cache)
    : IRequestHandler<UploadProductImageCommand, UploadProductImageDto>
{
    public async Task<UploadProductImageDto> Handle(UploadProductImageCommand request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct)
                      ?? throw new KeyNotFoundException("Product not found");

        if (request.Length <= 0)
            throw new ValidationFailedException("file", "The file is empty.");

        if (request.Length > ProductImage.MaxSizeBytes)
            throw new PayloadTooLargeException(
                $"Image is too large. Allowed: {FileSignatureValidator.AllowedImageTypesDescription}.");

        // The bytes decide the type, not the file name or the client's content type (FR-015).
        var contentType = await FileSignatureValidator.DetectImageContentTypeAsync(request.Content, ct);

        if (contentType is null)
            throw new UnsupportedFileTypeException(
                $"Unsupported image type. Allowed: {FileSignatureValidator.AllowedImageTypesDescription}.");

        var imageId = Guid.NewGuid();
        var objectKey = ProductImage.BuildObjectKey(product.Id, imageId);

        var becomesPrimary = request.IsPrimary || product.Images.Count == 0;

        if (becomesPrimary && product.Images.Any(x => x.IsPrimary))
        {
            // The one-primary index is checked row by row, so the old primary is cleared first.
            product.ClearPrimaryImage();
            await repo.SaveChangesAsync(ct);
        }

        // Store the object first, then write the row; undo the object if the row cannot be written (R8).
        var url = await storage.UploadStreamAsync(
            StorageBucket.Images, objectKey, request.Content, request.Length, contentType, ct);

        var image = new ProductImage(imageId, product.Id, objectKey, url, contentType, request.Length, 0, becomesPrimary);

        try
        {
            product.AddImage(image);
            await repo.AddImageAsync(image, ct);
            await repo.SaveChangesAsync(ct);
        }
        catch
        {
            await DeleteQuietly(objectKey);
            throw;
        }

        await cache.InvalidateAsync(product.Id);

        await publisher.PublishAsync(new ProductImageUploaded
        {
            ProductId = product.Id,
            ImageId = image.Id,
            ImageUrl = url,
            ObjectKey = objectKey,
            IsPrimary = image.IsPrimary
        }, "product.image.uploaded");

        return new UploadProductImageDto(image.Id, url, image.IsPrimary, image.SortOrder, contentType, request.Length);
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
