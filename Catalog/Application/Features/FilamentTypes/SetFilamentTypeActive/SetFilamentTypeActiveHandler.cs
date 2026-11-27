using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.FilamentTypes.SetFilamentTypeActive;

/// <summary>
/// Deactivated entries stay on the products that already use them but cannot be chosen for new ones (FR-021).
/// </summary>
public class SetFilamentTypeActiveHandler(IFilamentTypeRepository repo) : IRequestHandler<SetFilamentTypeActiveCommand>
{
    public async Task Handle(SetFilamentTypeActiveCommand request, CancellationToken ct)
    {
        var entry = await repo.GetByIdAsync(request.Id, ct)
                    ?? throw new KeyNotFoundException("Filament type not found");

        if (entry.SetActive(request.Active))
            await repo.SaveChangesAsync(ct);
    }
}
