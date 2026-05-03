using MediatR;

namespace Application.Features.Products.AddRelation;

public record AddProductRelationCommand(
    Guid ProductId,
    Guid RelatedProductId,
    string Type
) : IRequest<Guid>;