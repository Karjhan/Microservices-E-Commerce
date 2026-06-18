namespace Application.Contracts;

public class PartyV1
{
    public string Name { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}
