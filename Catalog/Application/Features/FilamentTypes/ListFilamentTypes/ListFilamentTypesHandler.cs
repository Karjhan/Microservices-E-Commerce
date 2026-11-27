using Application.DTOs;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.FilamentTypes.ListFilamentTypes;

public class ListFilamentTypesHandler(IFilamentTypeRepository repo) : IRequestHandler<ListFilamentTypesQuery, List<FilamentTypeDto>>
{
    public async Task<List<FilamentTypeDto>> Handle(ListFilamentTypesQuery request, CancellationToken ct)
    {
        var entries = await repo.ListAsync(request.IncludeInactive, ct);

        // Usage counts help admins decide between deleting and deactivating, so shoppers do not get them.
        var counts = request.IncludeInactive
            ? await repo.CountProductsUsingAsync(entries.Select(x => x.Id).ToList(), ct)
            : null;

        return entries
            .Select(x => FilamentTypeDto.FromEntity(x, counts?[x.Id]))
            .ToList();
    }
}
