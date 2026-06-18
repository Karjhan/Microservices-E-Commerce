using Application.Contracts;
using MediatR;

namespace Application.Features.Documents.GenerateReceipt;

public record GenerateReceiptCommand(
    ReceiptDocumentRequestV1 Request,
    Guid UserId,
    string? CorrelationId,
    string SourceRoutingKey = "payment.completed") : IRequest<GenerateReceiptResult>;

public record GenerateReceiptResult(Guid DocumentId, bool Succeeded, string? FailureReason = null);
