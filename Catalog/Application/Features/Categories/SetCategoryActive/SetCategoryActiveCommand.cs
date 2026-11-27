using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Categories.SetCategoryActive;

public record SetCategoryActiveCommand(Guid Id, bool Active) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => Active ? "category.activate" : "category.deactivate";
    string IAuditedRequest.AuditTargetType => "Category";
    string? IAuditedRequest.AuditTargetId => Id.ToString();
    object? IAuditedRequest.AuditDetails => null;
}
