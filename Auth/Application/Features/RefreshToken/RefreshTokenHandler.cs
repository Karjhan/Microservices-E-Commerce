using Domain.Abstractions.Authentication;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.RefreshToken;

public class RefreshTokenHandler
{
    private readonly AuthDbContext _db;
    private readonly IJwtProvider _jwt;
    private readonly IRefreshTokenService _refreshService;

    public RefreshTokenHandler(
        AuthDbContext db,
        IJwtProvider jwt,
        IRefreshTokenService refreshService)
    {
        _db = db;
        _jwt = jwt;
        _refreshService = refreshService;
    }

    public async Task<(string accessToken, string refreshToken)> Handle(RefreshTokenCommand command)
    {
        var now = DateTime.UtcNow;

        var tokens = await _db.RefreshTokens
            .Include(x => x.User)
            .Where(x => x.ExpiresAt > now)
            .ToListAsync();

        var stored = tokens.FirstOrDefault(t =>
            BCrypt.Net.BCrypt.Verify(command.RefreshToken, t.TokenHash));

        if (stored == null)
            throw new Exception("Invalid refresh token");

        if (stored.IsRevoked)
        {
            await RevokeFamily(stored.FamilyId);
            throw new Exception("Token reuse detected. Session revoked.");
        }

        stored.IsRevoked = true;

        var (newToken, rawToken) =
            _refreshService.Generate(stored.UserId, stored.FamilyId);

        stored.ReplacedByTokenId = newToken.Id;

        _db.RefreshTokens.Add(newToken);

        await _db.SaveChangesAsync();

        var jwt = _jwt.GenerateToken(
            stored.UserId,
            stored.User.Email,
            stored.User.Role
        );

        return (jwt, rawToken);
    }

    private async Task RevokeFamily(Guid familyId)
    {
        var tokens = await _db.RefreshTokens
            .Where(x => x.FamilyId == familyId && !x.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
        }

        await _db.SaveChangesAsync();
    }
}