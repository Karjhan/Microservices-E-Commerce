using Application.DTOs;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.GetRelations;

public class GetProductRelationsHandler(IProductRepository repo)
    : IRequestHandler<GetProductRelationsQuery, IReadOnlyList<RelationDto>>
{
    public async Task<IReadOnlyList<RelationDto>> Handle(GetProductRelationsQuery request, CancellationToken ct)
    {
        var relations = await repo.GetRelationsAsync(request.ProductId, ct);

        return relations.Select(x => new RelationDto
        {
            RelationId = x.Id,
            RelatedProductId = x.RelatedProductId,
            Type = x.Type
        }).ToList();
    }
}