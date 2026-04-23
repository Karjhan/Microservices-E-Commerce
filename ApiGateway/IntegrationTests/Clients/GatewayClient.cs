using System.Net.Http.Json;

namespace IntegrationTests.Clients;

public class GatewayClient
{
    private readonly HttpClient _client;

    public GatewayClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<HttpResponseMessage> Login(string email, string password)
    {
        return await _client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password
        });
    }

    public async Task<HttpResponseMessage> Register(string email, string password)
    {
        return await _client.PostAsJsonAsync("/auth/register", new
        {
            email,
            password
        });
    }
}