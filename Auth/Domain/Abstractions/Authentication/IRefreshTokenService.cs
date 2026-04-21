using Domain.Entities;

namespace Domain.Abstractions.Authentication;

public interface IRefreshTokenService
{
    (RefreshToken token, string rawToken) Generate(Guid userId, Guid? familyId = null);
    (RefreshToken token, string rawToken) GenerateWithRaw(Guid userId);
    bool Validate(string token, RefreshToken storedToken);
}