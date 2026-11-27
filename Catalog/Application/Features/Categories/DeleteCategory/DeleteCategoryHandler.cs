using Application.Common;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Categories.DeleteCategory;

public class DeleteCategoryHandler(ICategoryRepository repo) : IRequestHandler<DeleteCategoryCommand>
{
    public async Task Handle(DeleteCategoryCommand request, CancellationToken ct)
    {
        var entry = await repo.GetByIdAsync(request.Id, ct)
                    ?? throw new KeyNotFoundException("Category not found");

        // 409 in_use with the product count when anything still refers to it (FR-023).
        await ReferenceRules.EnsureNotInUseAsync(repo, entry, "category", ct);

        repo.Remove(entry);
        await repo.SaveChangesAsync(ct);
    }
}
