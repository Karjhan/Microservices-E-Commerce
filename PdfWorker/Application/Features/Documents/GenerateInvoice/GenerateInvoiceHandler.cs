using Application.Abstractions.Persistence;
using Application.Common;
using Application.Features.Documents;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.Documents.GenerateInvoice;

public class GenerateInvoiceHandler : IRequestHandler<GenerateInvoiceCommand, GenerateInvoiceResult>
{
    private readonly InvoiceDocumentRequestValidator _validator;
    private readonly IGenerateDocumentPipeline _pipeline;
    private readonly IDocumentRepository _repository;

    public GenerateInvoiceHandler(
        InvoiceDocumentRequestValidator validator,
        IGenerateDocumentPipeline pipeline,
        IDocumentRepository repository)
    {
        _validator = validator;
        _pipeline = pipeline;
        _repository = repository;
    }

    public async Task<GenerateInvoiceResult> Handle(GenerateInvoiceCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var idempotencyKey = IdempotencyKeyBuilder.ForInvoice(request.OrderId, request.PaymentId);

        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var reason = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return await HandleValidationFailureAsync(command, idempotencyKey, reason, cancellationToken);
        }

        var pipelineRequest = new GenerateDocumentRequest(
            DocumentType.Invoice,
            idempotencyKey,
            request.TemplateVersion,
            request,
            request.OrderId,
            request.PaymentId,
            command.UserId,
            command.SourceRoutingKey,
            command.CorrelationId);

        var result = await _pipeline.ExecuteAsync(pipelineRequest, cancellationToken);

        return new GenerateInvoiceResult(result.DocumentId, result.Status != GenerationStatus.Failed);
    }

    private async Task<GenerateInvoiceResult> HandleValidationFailureAsync(
        GenerateInvoiceCommand command,
        string idempotencyKey,
        string reason,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        Guid documentId;

        if (existing is null)
        {
            var failed = Document.CreatePending(
                DocumentType.Invoice,
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
            DocumentType.Invoice,
            idempotencyKey,
            command.Request.TemplateVersion,
            command.Request,
            command.Request.OrderId,
            command.Request.PaymentId,
            command.UserId,
            command.SourceRoutingKey,
            command.CorrelationId);

        await _pipeline.PublishFailureAsync(pipelineRequest, reason, cancellationToken);

        return new GenerateInvoiceResult(documentId, false, reason);
    }
}
