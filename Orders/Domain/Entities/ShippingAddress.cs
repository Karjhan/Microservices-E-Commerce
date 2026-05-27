namespace Domain.Entities;

public class ShippingAddress
{
    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public string Street { get; private set; } = default!;
    public string City { get; private set; } = default!;
    public string State { get; private set; } = default!;
    public string Country { get; private set; } = default!;
    public string ZipCode { get; private set; } = default!;

    public string? FullName { get; private set; }
    public string? Phone { get; private set; }

    public Order Order { get; private set; } = null!;

    private ShippingAddress() { }

    public ShippingAddress(
        Guid orderId,
        string street,
        string city,
        string state,
        string country,
        string zipCode,
        string? fullName = null,
        string? phone = null)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        Street = street;
        City = city;
        State = state;
        Country = country;
        ZipCode = zipCode;
        FullName = fullName;
        Phone = phone;
    }

    public void Update(
        string street,
        string city,
        string state,
        string country,
        string zipCode,
        string? fullName = null,
        string? phone = null)
    {
        Street = street;
        City = city;
        State = state;
        Country = country;
        ZipCode = zipCode;
        FullName = fullName;
        Phone = phone;
    }
}
