using Application.Abstractions.Persistence;
using Application.Common;
using Application.Features.Documents;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.Documents.GenerateCreditNote;

public class GenerateCreditNoteHandler : IRequestHandler<GenerateCreditNoteCommand, GenerateCreditNoteResult>
{
    private readonly CreditNoteDocumentRequestValidator _validator;
    private readonly IGenerateDocumentPipeline _pipeline;
    private readonly IDocumentRepository _repository;

    public GenerateCreditNoteHandler(
        CreditNoteDocumentRequestValidator validator,
        IGenerateDocumentPipeline pipeline,
        IDocumentRepository repository)
    {
        _validator = validator;
        _pipeline = pipeline;
        _repository = repository;
    }

    public async Task<GenerateCreditNoteResult> Handle(GenerateCreditNoteCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var idempotencyKey = IdempotencyKeyBuilder.ForCreditNote(
            request.PaymentId,
            request.CumulativeRefunded.Amount,
            request.RefundAmount.Amount);

        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var reason = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return await HandleValidationFailureAsync(command, idempotencyKey, reason, cancellationToken);
        }

        var pipelineRequest = new GenerateDocumentRequest(
            DocumentType.CreditNote,
            idempotencyKey,
            request.TemplateVersion,
            request,
            request.OrderId,
            request.PaymentId,
            command.UserId,
            command.SourceRoutingKey,
            command.CorrelationId);

        var result = await _pipeline.ExecuteAsync(pipelineRequest, cancellationToken);

        return new GenerateCreditNoteResult(result.DocumentId, result.Status != GenerationStatus.Failed);
    }

    private async Task<GenerateCreditNoteResult> HandleValidationFailureAsync(
        GenerateCreditNoteCommand command,
        string idempotencyKey,
        string reason,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        Guid documentId;

        if (existing is null)
        {
            var failed = Document.CreatePending(
                DocumentType.CreditNote,
                idempotencyKey,
                command.Request.TemplateVersion,
                command.Request.OrderId,
                command.Request.PaymentId,
                command.UserId,
                command.SourceRoutingKey,
                command.CorrelationId,
                null);
            failed.MarkFailed(reason);
            await _repository.AddAsync(failed, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            documentId = failed.Id;
        }
        else
        {
            documentId = existing.Id;
        }

        var pipelineRequest = new GenerateDocumentRequest(
            DocumentType.CreditNote,
            idempotencyKey,
            command.Request.TemplateVersion,
            command.Request,
            command.Request.OrderId,
            command.Request.PaymentId,
            command.UserId,
            command.SourceRoutingKey,
            command.CorrelationId);

        await _pipeline.PublishFailureAsync(pipelineRequest, reason, cancellationToken);

        return new GenerateCreditNoteResult(documentId, false, reason);
    }
}
