using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Audit;

public record GetAuditQuery(int? Page = null, int? PageSize = null) : IRequest<AuditPage>;