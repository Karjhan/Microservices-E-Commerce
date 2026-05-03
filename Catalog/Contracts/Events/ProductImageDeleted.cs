namespace Contracts.Events;

public class ProductImageDeleted
{
    public Guid ProductId { get; set; }
    public Guid ImageId { get; set; }
    public string ObjectKey { get; set; } = default!;
}