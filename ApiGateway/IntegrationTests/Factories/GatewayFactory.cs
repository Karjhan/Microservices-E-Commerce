using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace IntegrationTests.Factories;

public class GatewayFactory : WebApplicationFactory<Program>
{
    private readonly string _authUrl;

    public GatewayFactory(string authUrl)
    {
        _authUrl = authUrl;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:Clusters:auth-cluster:Destinations:auth:Address"] = _authUrl
            });
        });
    }
}