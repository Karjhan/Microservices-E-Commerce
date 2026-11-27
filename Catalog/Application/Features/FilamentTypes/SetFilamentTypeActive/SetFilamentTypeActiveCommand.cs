using MediatR;
using Shared.Security.Audit;

namespace Application.Features.FilamentTypes.SetFilamentTypeActive;

public record SetFilamentTypeActiveCommand(Guid Id, bool Active) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => Active ? "filament_type.activate" : "filament_type.deactivate";
    string IAuditedRequest.AuditTargetType => "FilamentType";
    string? IAuditedRequest.AuditTargetId => Id.ToString();
    object? IAuditedRequest.AuditDetails => null;
}
