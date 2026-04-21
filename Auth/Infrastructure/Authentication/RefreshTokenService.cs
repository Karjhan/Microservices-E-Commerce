using Domain.Abstractions.Authentication;
using Domain.Entities;

namespace Infrastructure.Authentication;

public class RefreshTokenService : IRefreshTokenService
{
    public (RefreshToken token, string rawToken) Generate(Guid userId, Guid? familyId = null)
    {
        var rawToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(rawToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            FamilyId = familyId ?? Guid.NewGuid(),
            IsRevoked = false
        };

        return (token, rawToken);
    }
    
    public (RefreshToken token, string rawToken) GenerateWithRaw(Guid userId)
    {
        var rawToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(rawToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        return (entity, rawToken);
    }

    public bool Validate(string token, RefreshToken storedToken)
    {
        return BCrypt.Net.BCrypt.Verify(token, storedToken.TokenHash);
    }
}