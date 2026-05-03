using MediatR;

namespace Application.Features.Products.DeleteAttribute;

public record DeleteProductAttributeCommand(
    Guid ProductId,
    Guid AttributeId
) : IRequest;