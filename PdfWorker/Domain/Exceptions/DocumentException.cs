namespace Domain.Exceptions;

public abstract class DocumentException : Exception
{
    public string Code { get; }
    public bool IsTransient { get; }

    protected DocumentException(string message, string code, bool isTransient = false)
        : base(message)
    {
        Code = code;
        IsTransient = isTransient;
    }
}

public sealed class DocumentValidationException : DocumentException
{
    public DocumentValidationException(string message)
        : base(message, "document.validation_failed", isTransient: false) { }
}

public sealed class DocumentNotFoundException : DocumentException
{
    public DocumentNotFoundException(Guid documentId)
        : base($"Document {documentId} not found", "document.not_found") { }
}

public sealed class DocumentRenderException : DocumentException
{
    public DocumentRenderException(string message, bool isTransient = false)
        : base(message, "document.render_failed", isTransient) { }
}

public sealed class DocumentStorageException : DocumentException
{
    public DocumentStorageException(string message, bool isTransient = true)
        : base(message, "document.storage_failed", isTransient) { }
}

public sealed class OrderEnrichmentException : DocumentException
{
    public OrderEnrichmentException(string message, bool isTransient = true)
        : base(message, "document.enrichment_failed", isTransient) { }
}
