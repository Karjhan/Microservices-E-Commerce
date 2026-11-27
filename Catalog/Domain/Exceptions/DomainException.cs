namespace Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }

    /// <summary>Extra machine-readable facts for the client, for example the product count of an in-use entry.</summary>
    public IReadOnlyDictionary<string, object?> Details { get; protected init; } = new Dictionary<string, object?>();
}

public sealed class ValidationFailedException : DomainException
{
    public ValidationFailedException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }

    public ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
        : base("validation_failed", "One or more fields are invalid.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

public sealed class ConflictException : DomainException
{
    public ConflictException(string code, string message) : base(code, message)
    {
    }

    public ConflictException(string code, string message, IReadOnlyDictionary<string, object?> details)
        : base(code, message)
    {
        Details = details;
    }

    /// <summary>A conflict caused by one field, for example a name that is already taken.</summary>
    public ConflictException(string code, string message, string field, string fieldMessage)
        : base(code, message)
    {
        Errors = new Dictionary<string, string[]> { [field] = [fieldMessage] };
    }

    public IReadOnlyDictionary<string, string[]>? Errors { get; }
}

public sealed class BusinessRuleException(string code, string message) : DomainException(code, message);

public sealed class PayloadTooLargeException(string message) : DomainException("payload_too_large", message);

public sealed class UnsupportedFileTypeException(string message) : DomainException("unsupported_media_type", message);
