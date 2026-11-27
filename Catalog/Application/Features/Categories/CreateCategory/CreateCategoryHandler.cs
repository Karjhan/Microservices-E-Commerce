using Application.Common;
using Domain.Entities;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Categories.CreateCategory;

public class CreateCategoryHandler(ICategoryRepository repo) : IRequestHandler<CreateCategoryCommand, Guid>
{
    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken ct)
    {
        var name = ReferenceRules.CleanName(request.Name);
        await ReferenceRules.EnsureNameAvailableAsync(repo, name, "category", null, ct);

        var entry = new Category(name);

        await repo.AddAsync(entry, ct);
        await repo.SaveChangesAsync(ct);

        return entry.Id;
    }
}
