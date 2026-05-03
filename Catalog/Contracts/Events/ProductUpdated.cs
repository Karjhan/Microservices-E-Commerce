namespace Contracts.Events;

public class ProductUpdated
{
    public Guid ProductId { get; init; }

    public string Name { get; init; } = default!;
    public string Slug { get; init; } = default!;

    public string ShortDescription { get; init; } = default!;
    public string LongDescription { get; init; } = default!;

    public decimal Price { get; init; }
    public string Currency { get; init; } = default!;

    public string Status { get; init; } = default!;

    public int DownloadCount { get; init; } = 0;
    
    public double AverageRating { get; init; } = 0;

    public List<string> Tags { get; init; } = new();

    public List<string> SupportedMaterials { get; init; } = new();
    public List<string> CompatiblePrinters { get; init; } = new();

    public DateTime UpdatedAt { get; init; }
}