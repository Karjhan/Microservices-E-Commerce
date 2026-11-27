using Application.Common;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.FilamentTypes.DeleteFilamentType;

public class DeleteFilamentTypeHandler(IFilamentTypeRepository repo) : IRequestHandler<DeleteFilamentTypeCommand>
{
    public async Task Handle(DeleteFilamentTypeCommand request, CancellationToken ct)
    {
        var entry = await repo.GetByIdAsync(request.Id, ct)
                    ?? throw new KeyNotFoundException("Filament type not found");

        // 409 in_use with the product count when anything still refers to it (FR-023).
        await ReferenceRules.EnsureNotInUseAsync(repo, entry, "filament type", ct);

        repo.Remove(entry);
        await repo.SaveChangesAsync(ct);
    }
}
