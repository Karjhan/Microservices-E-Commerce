using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Factories;
using IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntegrationTests.Tests.Auth;

public class CatalogTests : IClassFixture<TestEnvironment>
{
    private readonly HttpClient _client;

    public CatalogTests(TestEnvironment env)
    {
        var factory = new GatewayFactory(env.Auth.BaseUrl, env.Catalog.BaseUrl);

        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
    }

    [Fact]
    public async Task Create_Product_Should_Work()
    {
        var response = await _client.PostAsJsonAsync("/products", new
        {
            name = "Test",
            shortDescription = "S",
            longDescription = "L",
            price = 10,
            categoryId = Guid.NewGuid(),
            currency = "EUR",
            settings = new { },
            size = new { }
        });

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        json.Should().Contain("productId");
    }
}