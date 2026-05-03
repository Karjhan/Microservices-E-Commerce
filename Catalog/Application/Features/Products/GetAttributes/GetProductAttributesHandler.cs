using Application.DTOs;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.GetAttributes;

public class GetProductAttributesHandler(IProductRepository repo)
    : IRequestHandler<GetProductAttributesQuery, IReadOnlyList<AttributeDto>>
{
    public async Task<IReadOnlyList<AttributeDto>> Handle(GetProductAttributesQuery request, CancellationToken ct)
    {
        var attributes = await repo.GetAttributesAsync(request.ProductId, ct);

        return attributes.Select(x => new AttributeDto
        {
            AttributeId = x.Id,
            Key = x.Key,
            Value = x.Value
        }).ToList();
    }
}