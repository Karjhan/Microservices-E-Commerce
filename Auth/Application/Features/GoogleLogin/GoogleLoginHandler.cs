using Contracts.Events;
using Domain.Abstractions.Authentication;
using Domain.Abstractions.Messaging;
using Domain.Abstractions.Persistence;
using Domain.Entities;

namespace Application.Features.GoogleLogin;

public class GoogleLoginHandler
{
    private readonly IUserRepository _repo;
    private readonly IJwtProvider _jwt;
    private readonly IRefreshTokenService _refreshService;
    private readonly IGoogleTokenValidator _google;
    private readonly IEventPublisher _publisher;

    public GoogleLoginHandler(
        IUserRepository repo,
        IJwtProvider jwt,
        IRefreshTokenService refreshService,
        IGoogleTokenValidator google,
        IEventPublisher publisher)
    {
        _repo = repo;
        _jwt = jwt;
        _refreshService = refreshService;
        _google = google;
        _publisher =  publisher;
    }

    public async Task<(string accessToken, string refreshToken)> Handle(GoogleLoginCommand command)
    {
        var googleUser = await _google.ValidateAsync(command.IdToken);

        var email = googleUser.Email.ToLowerInvariant();

        var user = await _repo.GetByEmailAsync(email);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                NormalizedEmail = email.Trim().ToLowerInvariant(), 
                Provider = "Google",
                ProviderId = googleUser.ProviderId,
                Role = "Customer",
                CreatedAt = DateTime.UtcNow
            };

            await _repo.AddAsync(user);
        }

        if (user.Provider == "Local")
        {
            user.Provider = "Google";
            user.ProviderId = googleUser.ProviderId;
        }

        var jwt = _jwt.GenerateToken(user.Id, user.Email, user.Role);

        var (refreshToken, rawToken) = _refreshService.Generate(user.Id);

        await _repo.AddRefreshTokenAsync(refreshToken);
        
        await _publisher.PublishAsync(
            new UserLoggedInEvent(user.Id, user.Email),
            "user.logged_in"
        );

        return (jwt, rawToken);
    }
}