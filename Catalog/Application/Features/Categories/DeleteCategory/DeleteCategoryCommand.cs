using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Categories.DeleteCategory;

public record DeleteCategoryCommand(Guid Id) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "category.delete";
    string IAuditedRequest.AuditTargetType => "Category";
    string? IAuditedRequest.AuditTargetId => Id.ToString();
    object? IAuditedRequest.AuditDetails => null;
}
