using Application.Abstractions.Persistence;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Documents.ListDocuments;

public record ListDocumentsQuery(
    Guid? OrderId,
    Guid? PaymentId,
    DocumentType? DocumentType,
    GenerationStatus? Status,
    int Page = 1,
    int PageSize = 20) : IRequest<DocumentListResponse>;

public record DocumentListResponse(
    IReadOnlyList<DocumentDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public record DocumentDto(
    Guid Id,
    DocumentType DocumentType,
    string IdempotencyKey,
    string TemplateVersion,
    GenerationStatus Status,
    string? StorageObjectKey,
    string? StorageBucket,
    Guid OrderId,
    Guid PaymentId,
    Guid UserId,
    string? CorrelationId,
    string? FailureReason,
    long? FileSizeBytes,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    DateTime? FailedAt);

public class ListDocumentsHandler : IRequestHandler<ListDocumentsQuery, DocumentListResponse>
{
    private readonly IDocumentRepository _repository;

    public ListDocumentsHandler(IDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<DocumentListResponse> Handle(ListDocumentsQuery query, CancellationToken cancellationToken)
    {
        if (query.OrderId is null && query.PaymentId is null)
            throw new DocumentValidationException("At least one of orderId or paymentId is required");

        var (items, total) = await _repository.ListAsync(
            query.OrderId,
            query.PaymentId,
            query.DocumentType,
            query.Status,
            query.Page,
            query.PageSize,
            cancellationToken);

        return new DocumentListResponse(
            items.Select(Map).ToList(),
            query.Page,
            query.PageSize,
            total);
    }

    internal static DocumentDto Map(Document document) => new(
        document.Id,
        document.DocumentType,
        document.IdempotencyKey,
        document.TemplateVersion,
        document.Status,
        document.StorageObjectKey,
        document.StorageBucket,
        document.OrderId,
        document.PaymentId,
        document.UserId,
        document.CorrelationId,
        document.FailureReason,
        document.FileSizeBytes,
        document.CreatedAt,
        document.CompletedAt,
        document.FailedAt);
}
