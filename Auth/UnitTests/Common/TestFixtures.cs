using Domain.Abstractions.Authentication;
using Domain.Abstractions.Messaging;
using Domain.Abstractions.Persistence;
using Moq;

namespace UnitTests.Common;

public class TestFixtures
{
    public Mock<IUserRepository> UserRepo { get; } = new();
    public Mock<IJwtProvider> Jwt { get; } = new();
    public Mock<IRefreshTokenService> Refresh { get; } = new();
    public Mock<IEventPublisher> Publisher { get; } = new();
    public Mock<IGoogleTokenValidator> Google { get; } = new();
}