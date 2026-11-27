using Application.Common;
using Domain.Entities;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.FilamentTypes.CreateFilamentType;

public class CreateFilamentTypeHandler(IFilamentTypeRepository repo)
    : IRequestHandler<CreateFilamentTypeCommand, Guid>
{
    public async Task<Guid> Handle(CreateFilamentTypeCommand request, CancellationToken ct)
    {
        var name = ReferenceRules.CleanName(request.Name);
        await ReferenceRules.EnsureNameAvailableAsync(repo, name, "filament type", null, ct);

        var colors = FilamentTypeRules.ValidateDetails(request.DiameterMm, request.ColorOptions);
        var code = await FilamentTypeRules.ResolveCodeAsync(repo, request.Code, name, ct);

        var entry = new FilamentType(name, code, request.DiameterMm, colors);

        await repo.AddAsync(entry, ct);
        await repo.SaveChangesAsync(ct);

        return entry.Id;
    }
}
