using Application.DTOs;
using MediatR;

namespace Application.Features.FilamentTypes.ListFilamentTypes;

/// <summary>Active entries only, unless an admin asks for the inactive ones too.</summary>
public record ListFilamentTypesQuery(bool IncludeInactive) : IRequest<List<FilamentTypeDto>>;
