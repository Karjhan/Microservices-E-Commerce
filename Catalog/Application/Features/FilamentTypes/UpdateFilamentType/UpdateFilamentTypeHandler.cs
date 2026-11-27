using Application.Common;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.FilamentTypes.UpdateFilamentType;

public class UpdateFilamentTypeHandler(IFilamentTypeRepository repo, ProductCacheInvalidator cache)
    : IRequestHandler<UpdateFilamentTypeCommand>
{
    public async Task Handle(UpdateFilamentTypeCommand request, CancellationToken ct)
    {
        var entry = await repo.GetByIdAsync(request.Id, ct)
                    ?? throw new KeyNotFoundException("Filament type not found");

        var name = ReferenceRules.CleanName(request.Name);
        await ReferenceRules.EnsureNameAvailableAsync(repo, name, "filament type", entry.Id, ct);

        var colors = FilamentTypeRules.ValidateDetails(request.DiameterMm, request.ColorOptions);

        entry.Update(name, request.DiameterMm, colors);

        await repo.SaveChangesAsync(ct);

        // Cached product details carry this name.
        await cache.InvalidateAllAsync();
    }
}
