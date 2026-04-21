using Application.Features.GoogleLogin;
using Domain.Abstractions.Authentication;
using Domain.Entities;
using FluentAssertions;
using Moq;
using UnitTests.Common;

namespace UnitTests.Handlers;

public class GoogleLoginHandlerTests
{
    private readonly TestFixtures _f = new();

    [Fact]
    public async Task Should_Create_User_If_Not_Exists()
    {
        var email = "test@test.com";

        _f.Google.Setup(x => x.ValidateAsync(It.IsAny<string>()))
            .ReturnsAsync(new GoogleUserInfo(email, "provider-id"));

        _f.UserRepo.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync((User?)null);

        _f.Jwt.Setup(x => x.GenerateToken(It.IsAny<Guid>(), email, "Customer"))
            .Returns("jwt");

        _f.Refresh.Setup(x => x.Generate(It.IsAny<Guid>()))
            .Returns((new RefreshToken(), "refresh"));

        var handler = new GoogleLoginHandler(
            _f.UserRepo.Object,
            _f.Jwt.Object,
            _f.Refresh.Object,
            _f.Google.Object,
            _f.Publisher.Object
        );

        var result = await handler.Handle(new GoogleLoginCommand("token"));

        result.accessToken.Should().Be("jwt");

        _f.UserRepo.Verify(x => x.AddAsync(It.IsAny<User>()), Times.Once);
    }
}