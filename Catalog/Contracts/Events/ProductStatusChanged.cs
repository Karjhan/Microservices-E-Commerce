namespace Contracts.Events;

public class ProductStatusChanged
{
    public Guid ProductId { get; init; }

    public string PreviousStatus { get; init; } = default!;
    public string Status { get; init; } = default!;

    public bool IsVisible { get; init; }

    public DateTime ChangedAt { get; init; }
}
