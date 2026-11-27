using Domain.Enums;

namespace Domain.Commons;

public enum AdminProductSort
{
    Name,
    Price,
    CreatedAt,
    Status
}

/// <summary>Filters, sorting and paging for the admin product list (FR-020).</summary>
public class AdminProductFilter
{
    public const int MaxPageSize = 100;

    public string? Search { get; init; }
    public ProductStatus? Status { get; init; }
    public Guid? CategoryId { get; init; }

    /// <summary>Restricts the list to these products (used to compose the low-stock filter with Inventory).</summary>
    public IReadOnlyList<Guid>? ProductIds { get; init; }

    public AdminProductSort SortBy { get; init; } = AdminProductSort.CreatedAt;
    public bool Descending { get; init; } = true;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
