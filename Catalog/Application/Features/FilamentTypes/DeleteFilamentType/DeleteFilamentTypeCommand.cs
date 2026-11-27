using MediatR;
using Shared.Security.Audit;

namespace Application.Features.FilamentTypes.DeleteFilamentType;

public record DeleteFilamentTypeCommand(Guid Id) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "filament_type.delete";
    string IAuditedRequest.AuditTargetType => "FilamentType";
    string? IAuditedRequest.AuditTargetId => Id.ToString();
    object? IAuditedRequest.AuditDetails => null;
}
