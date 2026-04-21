using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Infrastructure.Persistence;
using IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Flows;

[Collection("Integration")]
public class AuthFlowTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;

    public AuthFlowTests(AuthApiFactory factory)
    {
        _client = factory.CreateClient();

        _scope = factory.Services.CreateScope();

        var db = _scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        db.Database.EnsureDeleted();
        db.Database.Migrate();
    }

    [Fact]
    public async Task Full_Register_Login_Flow_Should_Work()
    {
        var email = "test@test.com";
        var password = "password";

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", new
        {
            email,
            password
        });

        registerResponse.IsSuccessStatusCode.Should().BeTrue();

        var loginResponse = await _client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password
        });

        loginResponse.IsSuccessStatusCode.Should().BeTrue();

        var data = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        data!.AccessToken.Should().NotBeNull();
        data.RefreshToken.Should().NotBeNull();
    }
    
    [Fact]
    public async Task Refresh_Should_Return_New_Tokens()
    {
        var email = "test@test.com";
        var password = "password";

        await _client.PostAsJsonAsync("/auth/register", new { email, password });

        var login = await _client.PostAsJsonAsync("/auth/login", new { email, password });

        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>();

        var refresh = await _client.PostAsJsonAsync("/auth/refresh", new
        {
            refreshToken = tokens!.RefreshToken
        });

        refresh.IsSuccessStatusCode.Should().BeTrue();
    }
}


public class AuthResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = default!;

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = default!;
}