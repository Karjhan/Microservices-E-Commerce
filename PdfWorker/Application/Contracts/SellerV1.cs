namespace Application.Contracts;

public class SellerV1
{
    public string LegalName { get; init; } = default!;
    public string VatId { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string AddressLine1 { get; init; } = default!;
    public string City { get; init; } = default!;
    public string PostalCode { get; init; } = default!;
    public string Country { get; init; } = default!;
}
