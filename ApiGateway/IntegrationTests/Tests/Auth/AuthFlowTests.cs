using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Factories;
using IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntegrationTests.Tests.Auth;

public class AuthFlowTests : IClassFixture<TestEnvironment>
{
    private readonly HttpClient _client;

    public AuthFlowTests(TestEnvironment env)
    {
        var factory = new GatewayFactory(env.Auth.BaseUrl, env.Catalog.BaseUrl);
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
        Console.WriteLine($"Auth URL: {env.Auth.BaseUrl}");
    }

    [Fact]
    public async Task Register_Then_Login_Should_Work()
    {
        var register = await _client.PostAsJsonAsync("/auth/register", new
        {
            email = "test@test.com",
            password = "Password123!"
        });

        register.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/auth/login", new
        {
            email = "test@test.com",
            password = "Password123!"
        });

        login.EnsureSuccessStatusCode();

        var json = await login.Content.ReadAsStringAsync();

        json.Should().Contain("accessToken");
        json.Should().Contain("refreshToken");
    }
}