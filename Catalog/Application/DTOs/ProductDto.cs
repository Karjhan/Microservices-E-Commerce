using Domain.Entities;

namespace Application.DTOs;

public class ProductDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = default!;
    public string Slug { get; init; } = default!;

    public string ShortDescription { get; init; } = default!;
    public string LongDescription { get; init; } = default!;

    public decimal Price { get; init; }
    public string Currency { get; init; } = default!;

    public string Status { get; init; } = default!;
    public Guid CategoryId { get; init; }

    public PrintSettingsDto Settings { get; init; } = new();
    public DimensionsDto Size { get; init; } = new();

    public List<string> Tags { get; init; } = new();
    public List<AttributeDto> Attributes { get; init; } = new();
    public List<RelationDto> RelatedProducts { get; init; } = new();

    public List<string> SupportedMaterials { get; init; } = new();
    public List<string> CompatiblePrinters { get; init; } = new();

    public List<ProductImageDto> Images { get; init; } = new();

    /// <summary>Metadata only; the short-lived download link comes from GET /products/{id}/model.</summary>
    public ProductModelDto? Model { get; init; }

    public int DownloadCount { get; init; }
    public double AverageRating { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public static ProductDto FromEntity(Product product)
    {
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            ShortDescription = product.ShortDescription,
            LongDescription = product.LongDescription,
            Price = product.Price,
            Currency = product.Currency,
            Status = product.Status.ToString(),
            CategoryId = product.CategoryId,
            Settings = new PrintSettingsDto
            {
                LayerHeight = product.Settings.LayerHeight,
                InfillPercentage = product.Settings.InfillPercentage,
                NozzleSize = product.Settings.NozzleSize,
                PrintTimeMinutes = product.Settings.PrintTimeMinutes,
                FilamentUsedGrams = product.Settings.FilamentUsedGrams,
                SupportsRequired = product.Settings.SupportsRequired
            },
            Size = new DimensionsDto
            {
                WidthMm = product.Size.WidthMm,
                HeightMm = product.Size.HeightMm,
                DepthMm = product.Size.DepthMm
            },
            Tags = product.Tags.ToList(),
            Attributes = product.Attributes.Select(a => new AttributeDto
            {
                AttributeId = a.Id,
                Key = a.Key,
                Value = a.Value
            }).ToList(),
            RelatedProducts = product.RelatedProducts.Select(r => new RelationDto
            {
                RelationId = r.Id,
                RelatedProductId = r.RelatedProductId,
                Type = r.Type
            }).ToList(),
            SupportedMaterials = product.SupportedMaterials.Select(x => x.ToString()).ToList(),
            CompatiblePrinters = product.CompatiblePrinters.Select(x => x.ToString()).ToList(),
            Images = product.Images
                .OrderBy(x => x.SortOrder)
                .Select(ProductImageDto.FromEntity)
                .ToList(),
            Model = product.Model is null ? null : ProductModelDto.FromEntity(product.Model),
            DownloadCount = product.DownloadCount,
            AverageRating = product.AverageRating,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }
}