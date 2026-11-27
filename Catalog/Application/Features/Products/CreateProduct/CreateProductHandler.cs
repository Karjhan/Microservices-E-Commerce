using Application.Abstractions.Messaging;
using Application.Common;
using Contracts.Events;
using Domain.Entities;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.CreateProduct;

public class CreateProductHandler(
    IProductRepository repo,
    IEventPublisher publisher,
    ICacheService cache)
    : IRequestHandler<CreateProductCommand, Guid>
{
    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken ct)
    {
        await ProductRules.ValidateAsync(repo, null, request.Name, request.Price, request.Currency, ct);

        // New products start as drafts; an admin publishes them when ready (FR-016).
        var product = new Product(
            request.Name.Trim(),
            request.ShortDescription,
            request.LongDescription,
            request.Price,
            request.CategoryId
        );

        product.SetCurrency(ProductRules.NormalizeCurrency(request.Currency));
        product.SetSettings(request.Settings);
        product.SetSize(request.Size);

        if (request.Tags is not null)
            product.SetTags(request.Tags);

        if (request.SupportedMaterials is not null)
            product.SetSupportedMaterials(request.SupportedMaterials);

        if (request.CompatiblePrinters is not null)
            product.SetCompatiblePrinters(request.CompatiblePrinters);

        await repo.AddAsync(product, ct);
        await repo.SaveChangesAsync(ct);

        await cache.RemoveByPrefixAsync("products:");

        await publisher.PublishAsync(new ProductCreated
        {
            ProductId = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            ShortDescription = product.ShortDescription,
            LongDescription = product.LongDescription,
            Price = product.Price,
            Currency = product.Currency,
            CategoryId = product.CategoryId,
            Status = product.Status.ToString(),
            Tags = product.Tags.ToList(),
            Attributes = product.Attributes.Select(a => new ProductAttributeDto
            {
                Key = a.Key,
                Value = a.Value
            }).ToList(),
            SupportedMaterials = product.SupportedMaterials.Select(x => x.ToString()).ToList(),
            CompatiblePrinters = product.CompatiblePrinters.Select(x => x.ToString()).ToList(),
            CreatedAt = product.CreatedAt
        }, "product.created");

        return product.Id;
    }
}