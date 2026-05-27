using MediatR;

namespace Application.Features.Orders.UpdateShippingAddress;

public record UpdateShippingAddressCommand(
    Guid OrderId,
    string Street,
    string City,
    string State,
    string Country,
    string ZipCode,
    string? FullName,
    string? Phone) : IRequest;
