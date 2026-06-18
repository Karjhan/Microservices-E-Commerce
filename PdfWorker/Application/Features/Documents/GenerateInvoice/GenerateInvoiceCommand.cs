using Application.Contracts;
using MediatR;

namespace Application.Features.Documents.GenerateInvoice;

public record GenerateInvoiceCommand(
    InvoiceDocumentRequestV1 Request,
    Guid UserId,
    string? CorrelationId,
    string SourceRoutingKey = "payment.completed") : IRequest<GenerateInvoiceResult>;

public record GenerateInvoiceResult(Guid DocumentId, bool Succeeded, string? FailureReason = null);
