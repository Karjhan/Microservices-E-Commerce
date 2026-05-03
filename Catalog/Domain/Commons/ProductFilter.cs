using Domain.Enums;

namespace Domain.Commons;

public class ProductFilter
{
    public Guid? CategoryId { get; init; }

    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }

    public List<string>? Tags { get; init; }

    public MaterialType? Material { get; init; }
    public PrinterType? PrinterType { get; init; }

    public string? Search { get; init; }

    public ProductStatus? Status { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}