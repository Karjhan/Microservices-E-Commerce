using MediatR;

namespace Application.Features.Products.DeleteRelation;

public record DeleteProductRelationCommand(
    Guid ProductId,
    Guid RelationId
) : IRequest;