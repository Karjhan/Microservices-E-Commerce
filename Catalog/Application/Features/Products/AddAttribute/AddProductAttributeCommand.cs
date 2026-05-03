using MediatR;

namespace Application.Features.Products.AddAttribute;

public record AddProductAttributeCommand(
    Guid ProductId,
    string Key,
    string Value
) : IRequest<Guid>;