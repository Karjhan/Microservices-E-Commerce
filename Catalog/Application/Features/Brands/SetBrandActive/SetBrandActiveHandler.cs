using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Brands.SetBrandActive;

/// <summary>
/// Deactivated entries stay on the products that already use them but cannot be chosen for new ones (FR-021).
/// </summary>
public class SetBrandActiveHandler(IBrandRepository repo) : IRequestHandler<SetBrandActiveCommand>
{
    public async Task Handle(SetBrandActiveCommand request, CancellationToken ct)
    {
        var entry = await repo.GetByIdAsync(request.Id, ct)
                    ?? throw new KeyNotFoundException("Brand not found");

        if (entry.SetActive(request.Active))
            await repo.SaveChangesAsync(ct);
    }
}
