using MediatR;
using Shared.Security.Audit;

namespace Application.Features.FilamentTypes.UpdateFilamentType;

/// <summary>Replaces name, diameter and colors. The code stays as it was created.</summary>
public record UpdateFilamentTypeCommand(
    Guid Id,
    string Name,
    decimal? DiameterMm,
    string[]? ColorOptions
) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "filament_type.update";
    string IAuditedRequest.AuditTargetType => "FilamentType";
    string? IAuditedRequest.AuditTargetId => Id.ToString();
    object? IAuditedRequest.AuditDetails => new { name = Name, diameterMm = DiameterMm };
}
