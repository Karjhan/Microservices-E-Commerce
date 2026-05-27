namespace Domain.Exceptions;

public abstract class OrderException : Exception
{
    public string Code { get; }

    protected OrderException(string message, string code) : base(message)
    {
        Code = code;
    }
}

public sealed class OrderNotFoundException : OrderException
{
    public OrderNotFoundException(Guid orderId)
        : base($"Order {orderId} not found", "order.not_found") { }
}

public sealed class InvalidOrderStateException : OrderException
{
    public InvalidOrderStateException(string message)
        : base(message, "order.invalid_state") { }
}
