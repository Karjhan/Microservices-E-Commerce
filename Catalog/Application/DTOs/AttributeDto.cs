namespace Application.DTOs;

public class AttributeDto
{
    public Guid AttributeId { get; init; }
    public string Key { get; init; } = default!;
    public string Value { get; init; } = default!;
}