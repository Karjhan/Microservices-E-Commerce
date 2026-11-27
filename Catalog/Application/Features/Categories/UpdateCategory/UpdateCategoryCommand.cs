using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Categories.UpdateCategory;

public record UpdateCategoryCommand(Guid Id, string Name) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "category.update";
    string IAuditedRequest.AuditTargetType => "Category";
    string? IAuditedRequest.AuditTargetId => Id.ToString();
    object? IAuditedRequest.AuditDetails => new { name = Name };
}
