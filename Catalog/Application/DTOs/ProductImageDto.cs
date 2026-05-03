namespace Application.DTOs;

public class ProductImageDto
{
    public Guid ImageId { get; init; }
    public string ObjectKey { get; init; } = default!;
    public string Url { get; init; } = default!;
    public bool IsPrimary { get; init; }
}