using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Categories.SetCategoryActive;

/// <summary>
/// Deactivated entries stay on the products that already use them but cannot be chosen for new ones (FR-021).
/// </summary>
public class SetCategoryActiveHandler(ICategoryRepository repo) : IRequestHandler<SetCategoryActiveCommand>
{
    public async Task Handle(SetCategoryActiveCommand request, CancellationToken ct)
    {
        var entry = await repo.GetByIdAsync(request.Id, ct)
                    ?? throw new KeyNotFoundException("Category not found");

        if (entry.SetActive(request.Active))
            await repo.SaveChangesAsync(ct);
    }
}
