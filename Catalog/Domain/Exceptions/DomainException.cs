namespace Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
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

public sealed class ConflictException(string code, string message) : DomainException(code, message);

public sealed class BusinessRuleException(string code, string message) : DomainException(code, message);

public sealed class PayloadTooLargeException(string message) : DomainException("payload_too_large", message);

public sealed class UnsupportedFileTypeException(string message) : DomainException("unsupported_media_type", message);
