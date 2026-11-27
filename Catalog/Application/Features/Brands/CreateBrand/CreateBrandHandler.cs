using Application.Common;
using Domain.Entities;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Brands.CreateBrand;

public class CreateBrandHandler(IBrandRepository repo) : IRequestHandler<CreateBrandCommand, Guid>
{
    public async Task<Guid> Handle(CreateBrandCommand request, CancellationToken ct)
    {
        var name = ReferenceRules.CleanName(request.Name);
        await ReferenceRules.EnsureNameAvailableAsync(repo, name, "brand", null, ct);

        var entry = new Brand(name);

        await repo.AddAsync(entry, ct);
        await repo.SaveChangesAsync(ct);

        return entry.Id;
    }
}
