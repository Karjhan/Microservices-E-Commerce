using System.Text.Json;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Rendering;
using Application.Abstractions.Storage;
using PdfWorker.Contracts.Events;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Application.Features.Documents;

public record GenerateDocumentRequest(
    DocumentType DocumentType,
    string IdempotencyKey,
    string TemplateVersion,
    object RequestContract,
    Guid OrderId,
    Guid PaymentId,
    Guid UserId,
    string SourceRoutingKey,
    string? CorrelationId);

public record GenerateDocumentResult(
    Guid DocumentId,
    GenerationStatus Status,
    bool IsIdempotentReplay,
    string? StorageBucket = null,
    string? StorageObjectKey = null,
    long? FileSizeBytes = null);

public interface IGenerateDocumentPipeline
{
    Task<GenerateDocumentResult> ExecuteAsync(GenerateDocumentRequest request, CancellationToken cancellationToken = default);
    Task PublishFailureAsync(GenerateDocumentRequest request, string failureReason, CancellationToken cancellationToken = default);
}

public class GenerateDocumentPipeline : IGenerateDocumentPipeline
{
    private readonly IDocumentRepository _repository;
    private readonly IPdfRenderer _renderer;
    private readonly IObjectStorageService _storage;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<GenerateDocumentPipeline> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public GenerateDocumentPipeline(
        IDocumentRepository repository,
        IPdfRenderer renderer,
        IObjectStorageService storage,
        IEventPublisher publisher,
        ILogger<GenerateDocumentPipeline> logger)
    {
        _repository = repository;
        _renderer = renderer;
        _storage = storage;
        _publisher = publisher;
        _logger = logger;

        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = false,
                ShouldHandle = new PredicateBuilder().Handle<DocumentException>(ex => ex.IsTransient)
            })
            .Build();
    }

    public async Task<GenerateDocumentResult> ExecuteAsync(
        GenerateDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status == GenerationStatus.Completed)
            {
                await PublishGeneratedAsync(existing, cancellationToken);
                return new GenerateDocumentResult(
                    existing.Id,
                    existing.Status,
                    IsIdempotentReplay: true,
                    existing.StorageBucket,
                    existing.StorageObjectKey,
                    existing.FileSizeBytes);
            }

            if (existing.Status == GenerationStatus.Failed)
            {
                return new GenerateDocumentResult(existing.Id, existing.Status, IsIdempotentReplay: true);
            }
        }

        var contractJson = JsonSerializer.Serialize(request.RequestContract, JsonOptions);
        var document = existing ?? Document.CreatePending(
            request.DocumentType,
            request.IdempotencyKey,
            request.TemplateVersion,
            request.OrderId,
            request.PaymentId,
            request.UserId,
            request.SourceRoutingKey,
            request.CorrelationId,
            contractJson);

        if (existing is null)
        {
            await _repository.AddAsync(document, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        try
        {
            await _retryPipeline.ExecuteAsync(async ct =>
            {
                var pdfBytes = await _renderer.RenderAsync(request.TemplateVersion, request.RequestContract, ct);
                var (bucket, objectKey, fileSize) = await _storage.UploadAsync(
                    pdfBytes,
                    request.DocumentType.ToString(),
                    "application/pdf",
                    ct);

                document.MarkCompleted(bucket, objectKey, "application/pdf", fileSize);
                await _repository.UpdateAsync(document, cancellationToken);
                await _repository.SaveChangesAsync(cancellationToken);
            }, cancellationToken);

            await PublishGeneratedAsync(document, cancellationToken);

            return new GenerateDocumentResult(
                document.Id,
                document.Status,
                IsIdempotentReplay: false,
                document.StorageBucket,
                document.StorageObjectKey,
                document.FileSizeBytes);
        }
        catch (Exception ex)
        {
            var reason = ex is DocumentException dex ? dex.Message : ex.Message;
            var isTransient = ex is DocumentException { IsTransient: true };

            if (!isTransient)
            {
                document.MarkFailed(reason);
                await _repository.UpdateAsync(document, cancellationToken);
                await _repository.SaveChangesAsync(cancellationToken);
                await PublishFailureAsync(request, reason, cancellationToken);
            }

            throw;
        }
    }

    public async Task PublishFailureAsync(
        GenerateDocumentRequest request,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        var failureEvent = new DocumentGenerationFailed
        {
            DocumentType = request.DocumentType.ToString(),
            IdempotencyKey = request.IdempotencyKey,
            OrderId = request.OrderId,
            PaymentId = request.PaymentId,
            UserId = request.UserId,
            SourceRoutingKey = request.SourceRoutingKey,
            FailureReason = failureReason,
            CorrelationId = request.CorrelationId,
            FailedAt = DateTime.UtcNow
        };

        await _publisher.PublishAsync(
            failureEvent,
            "document.generation.failed",
            request.CorrelationId,
            cancellationToken);
    }

    private async Task PublishGeneratedAsync(Document document, CancellationToken cancellationToken)
    {
        if (document.Status != GenerationStatus.Completed
            || document.StorageBucket is null
            || document.StorageObjectKey is null
            || document.FileSizeBytes is null)
        {
            return;
        }

        var generatedEvent = new DocumentGenerated
        {
            DocumentId = document.Id,
            DocumentType = document.DocumentType.ToString(),
            IdempotencyKey = document.IdempotencyKey,
            TemplateVersion = document.TemplateVersion,
            OrderId = document.OrderId,
            PaymentId = document.PaymentId,
            UserId = document.UserId,
            StorageBucket = document.StorageBucket,
            StorageObjectKey = document.StorageObjectKey,
            ContentType = document.ContentType ?? "application/pdf",
            FileSizeBytes = document.FileSizeBytes.Value,
            CorrelationId = document.CorrelationId,
            GeneratedAt = document.CompletedAt ?? DateTime.UtcNow
        };

        await _publisher.PublishAsync(
            generatedEvent,
            "document.generated",
            document.CorrelationId,
            cancellationToken);
    }
}
