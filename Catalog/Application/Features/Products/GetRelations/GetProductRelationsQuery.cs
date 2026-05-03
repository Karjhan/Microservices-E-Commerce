using Application.DTOs;
using MediatR;

namespace Application.Features.Products.GetRelations;

public record GetProductRelationsQuery(Guid ProductId) : IRequest<IReadOnlyList<RelationDto>>;