namespace Domain.Exceptions;

public abstract class AppException : Exception
{
    public string Code { get; }

    protected AppException(string message, string code) : base(message)
    {
        Code = code;
    }
}