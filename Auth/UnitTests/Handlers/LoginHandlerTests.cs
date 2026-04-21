using Application.Features.Login;
using Domain.Entities;
using FluentAssertions;
using Moq;
using UnitTests.Common;

namespace UnitTests.Handlers;

public class LoginHandlerTests
{
    private readonly TestFixtures _f = new();

    [Fact]
    public async Task Should_Login_When_Credentials_Are_Valid()
    {
        var password = "password";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            PasswordHash = hash,
            Role = "Customer"
        };

        _f.UserRepo.Setup(x => x.GetByEmailAsync(user.Email))
            .ReturnsAsync(user);

        _f.Jwt.Setup(x => x.GenerateToken(user.Id, user.Email, user.Role))
            .Returns("jwt");

        _f.Refresh.Setup(x => x.Generate(user.Id))
            .Returns((new RefreshToken(), "refresh"));

        var handler = new LoginHandler(
            _f.UserRepo.Object,
            _f.Jwt.Object,
            _f.Refresh.Object,
            _f.Publisher.Object
        );

        var result = await handler.Handle(new LoginCommand(user.Email, password));

        result.accessToken.Should().Be("jwt");
        result.refreshToken.Should().Be("refresh");

        _f.Publisher.Verify(x =>
                x.PublishAsync(It.IsAny<object>(), "user.logged_in", default),
            Times.Once);
    }

    [Fact]
    public async Task Should_Throw_When_User_Not_Found()
    {
        _f.UserRepo.Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        var handler = new LoginHandler(
            _f.UserRepo.Object,
            _f.Jwt.Object,
            _f.Refresh.Object,
            _f.Publisher.Object
        );

        await Assert.ThrowsAsync<Exception>(() =>
            handler.Handle(new LoginCommand("x", "y")));
    }
}