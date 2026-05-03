namespace Contracts.Events;

public class ProductCreated
{
    public Guid ProductId { get; init; }

    public string Name { get; init; } = default!;
    public string Slug { get; init; } = default!;

    public string ShortDescription { get; init; } = default!;
    public string LongDescription { get; init; } = default!;

    public decimal Price { get; init; }
    public string Currency { get; init; } = default!;

    public Guid CategoryId { get; init; }

    public string Status { get; init; } = default!;

    public List<string> Tags { get; init; } = new();
    
    public int DownloadCount { get; init; } = 0;
    
    public double AverageRating { get; init; } = 0;

    public List<ProductAttributeDto> Attributes { get; init; } = new();

    public List<string> SupportedMaterials { get; init; } = new();
    public List<string> CompatiblePrinters { get; init; } = new();

    public DateTime CreatedAt { get; init; }
}

public class ProductAttributeDto
{
    public string Key { get; init; } = default!;
    public string Value { get; init; } = default!;
}