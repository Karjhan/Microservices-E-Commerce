namespace Domain.Abstractions.Authentication;

public interface IJwtProvider
{
    string GenerateToken(Guid userId, string email, string role);
}