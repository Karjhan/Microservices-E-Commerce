namespace Contracts.Events;

public class ProductImageUploaded
{
    public Guid ProductId { get; init; }
    public Guid ImageId { get; init; }

    public string ImageUrl { get; init; } = default!;
    public string ObjectKey { get; init; } = default!;

    public bool IsPrimary { get; init; }
}