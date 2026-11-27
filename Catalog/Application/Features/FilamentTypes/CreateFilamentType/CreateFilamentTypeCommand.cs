using MediatR;
using Shared.Security.Audit;

namespace Application.Features.FilamentTypes.CreateFilamentType;

/// <summary>The code is optional; it is derived from the name when left out, and cannot be changed later.</summary>
public record CreateFilamentTypeCommand(
    string Name,
    string? Code,
    decimal? DiameterMm,
    string[]? ColorOptions
) : IRequest<Guid>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "filament_type.create";
    string IAuditedRequest.AuditTargetType => "FilamentType";
    string? IAuditedRequest.AuditTargetId => null; // the new id is taken from the response
    object? IAuditedRequest.AuditDetails => new { name = Name, code = Code, diameterMm = DiameterMm };
}
