using Contracts.Events;
using Domain.Abstractions.Authentication;
using Domain.Abstractions.Messaging;
using Domain.Abstractions.Persistence;

namespace Application.Features.Login;

public class LoginHandler
{
    private readonly IUserRepository _repo;
    private readonly IJwtProvider _jwt;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IEventPublisher _publisher;

    public LoginHandler(IUserRepository repo, IJwtProvider jwt, IRefreshTokenService refreshTokenService, IEventPublisher publisher)
    {
        _repo = repo;
        _jwt = jwt;
        _refreshTokenService = refreshTokenService;
        _publisher = publisher;
    }

    public async Task<(string accessToken, string refreshToken)> Handle(LoginCommand command)
    {
        var user = await _repo.GetByEmailAsync(command.Email);
        if (user == null)
            throw new Exception("Invalid credentials");

        var valid = BCrypt.Net.BCrypt.Verify(command.Password, user.PasswordHash);
        if (!valid)
            throw new Exception("Invalid credentials");

        var jwt = _jwt.GenerateToken(user.Id, user.Email, user.Role);

        var (refreshEntity, rawToken) = _refreshTokenService.Generate(user.Id);

        await _repo.AddRefreshTokenAsync(refreshEntity);
        
        await _publisher.PublishAsync(
            new UserLoggedInEvent(user.Id, user.Email),
            "user.logged_in"
        );

        return (jwt, rawToken);
    }
}