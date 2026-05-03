using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.DTOs;
using Contracts.Events;
using Domain.Entities;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.UploadImage;

public class UploadProductImageHandler(
    IProductRepository repo,
    IFileStorageService storage,
    IEventPublisher publisher)
    : IRequestHandler<UploadProductImageCommand, UploadProductImageDto>
{
    public async Task<UploadProductImageDto> Handle(UploadProductImageCommand request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct);

        if (product is null)
            throw new KeyNotFoundException("Product not found");

        var objectKey = $"products/{product.Id}/{Guid.NewGuid()}-{request.FileName}";

        var url = await storage.UploadAsync(
            request.FilePath,
            objectKey,
            request.ContentType);

        var image = new ProductImage(product.Id, objectKey, url, request.IsPrimary);

        product.AddImage(image);
        await repo.AddImageAsync(image, ct);

        await repo.SaveChangesAsync(ct);

        await publisher.PublishAsync(new ProductImageUploaded
        {
            ProductId = product.Id,
            ImageUrl = url,
            ObjectKey = objectKey
        }, "product.image.uploaded");

        return new UploadProductImageDto(image.Id, url);
    }
}