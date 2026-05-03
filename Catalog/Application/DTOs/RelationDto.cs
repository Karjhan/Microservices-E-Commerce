namespace Application.DTOs;

public class RelationDto
{
    public Guid RelationId { get; init; }
    public Guid RelatedProductId { get; init; }
    public string Type { get; init; } = default!;
}