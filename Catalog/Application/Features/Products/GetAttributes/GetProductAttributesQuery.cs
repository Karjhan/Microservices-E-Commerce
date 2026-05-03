using Application.DTOs;
using MediatR;

namespace Application.Features.Products.GetAttributes;

public record GetProductAttributesQuery(Guid ProductId) : IRequest<IReadOnlyList<AttributeDto>>;