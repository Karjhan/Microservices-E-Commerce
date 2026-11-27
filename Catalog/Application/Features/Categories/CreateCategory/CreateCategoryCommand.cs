using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Categories.CreateCategory;

public record CreateCategoryCommand(string Name) : IRequest<Guid>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "category.create";
    string IAuditedRequest.AuditTargetType => "Category";
    string? IAuditedRequest.AuditTargetId => null; // the new id is taken from the response
    object? IAuditedRequest.AuditDetails => new { name = Name };
}
