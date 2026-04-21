namespace Domain.Exceptions;

public sealed class InvalidCredentialsException : AppException
{
    public InvalidCredentialsException()
        : base("Invalid email or password", "auth.invalid_credentials")
    {
    }
}