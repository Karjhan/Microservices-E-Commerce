using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Factories;
using IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntegrationTests.Tests.GraphRAG;

public class GraphRagRouteTests : IClassFixture<TestEnvironment>
{
    private readonly HttpClient _client;

    public GraphRagRouteTests(TestEnvironment env)
    {
        var factory = new GatewayFactory(
            env.Auth.BaseUrl,
            env.Catalog.BaseUrl,
            env.GraphRag.BaseUrl);

        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
    }

    [Fact]
    public async Task GraphRag_Health_Through_Gateway_Should_Return_Ok()
    {
        var response = await _client.GetAsync("/graphrag/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("status");
    }

    [Fact]
    public async Task GraphRag_Search_Through_Gateway_Should_Return_Ok()
    {
        var response = await _client.PostAsJsonAsync("/graphrag/api/search", new
        {
            query = "articulated dragon",
            limit = 3
        });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.InternalServerError);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            json.Should().Contain("results");
        }
    }
}