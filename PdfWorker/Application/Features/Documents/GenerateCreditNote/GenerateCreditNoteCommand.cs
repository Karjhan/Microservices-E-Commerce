using Application.Contracts;
using MediatR;

namespace Application.Features.Documents.GenerateCreditNote;

public record GenerateCreditNoteCommand(
    CreditNoteDocumentRequestV1 Request,
    Guid UserId,
    string? CorrelationId,
    string SourceRoutingKey = "payment.refunded") : IRequest<GenerateCreditNoteResult>;

public record GenerateCreditNoteResult(Guid DocumentId, bool Succeeded, string? FailureReason = null);
