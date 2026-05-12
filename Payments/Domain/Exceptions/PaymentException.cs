namespace Domain.Exceptions;

public abstract class PaymentException : Exception
{
    public string Code { get; }

    protected PaymentException(string message, string code) : base(message)
    {
        Code = code;
    }
}

public sealed class PaymentNotFoundException : PaymentException
{
    public PaymentNotFoundException(Guid paymentId)
        : base($"Payment {paymentId} not found", "payment.not_found") { }
}

public sealed class InvalidPaymentStateException : PaymentException
{
    public InvalidPaymentStateException(string message)
        : base(message, "payment.invalid_state") { }
}

public sealed class DuplicatePaymentException : PaymentException
{
    public DuplicatePaymentException(string idempotencyKey)
        : base($"Payment with idempotency key '{idempotencyKey}' already exists", "payment.duplicate") { }
}

public sealed class InvalidRefundException : PaymentException
{
    public InvalidRefundException(string message)
        : base(message, "payment.invalid_refund") { }
}
