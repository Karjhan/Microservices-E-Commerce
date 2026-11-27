using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Factories;
using IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Shared.Security.TestSupport;

namespace IntegrationTests.Tests.Payments;

public class PaymentsTests : IClassFixture<TestEnvironment>
{
    private readonly HttpClient _client;

    public PaymentsTests(TestEnvironment env)
    {
        var factory = new GatewayFactory(
            env.Auth.BaseUrl,
            env.Catalog.BaseUrl,
            env.Payments.BaseUrl);

        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
    }

    [Fact]
    public async Task Create_Payment_Should_Work_Via_Gateway()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _client.AsCustomer(userId);

        var request = new
        {
            orderId = Guid.NewGuid(),
            userId,
            amount = 49.99m,
            currency = "EUR",
            method = 0,
            idempotencyKey = Guid.NewGuid().ToString(),
            paymentToken = "pm_test_token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/payments", request);

        // Assert
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("paymentId");
    }
}