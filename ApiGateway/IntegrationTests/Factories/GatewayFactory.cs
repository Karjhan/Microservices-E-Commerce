using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace IntegrationTests.Factories;

public class GatewayFactory : WebApplicationFactory<Program>
{
    private readonly string _authUrl;
    
    private readonly string _catalogUrl;
    
    private readonly string? _paymentsUrl;

    private readonly string? _graphragUrl;
    public GatewayFactory(string authUrl, string catalogUrl, string? paymentsUrl = null, string? graphragUrl = null)
    {
        _authUrl = authUrl;
        _catalogUrl = catalogUrl;
        _paymentsUrl = paymentsUrl;
        _graphragUrl = graphragUrl;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ReverseProxy:Clusters:auth-cluster:Destinations:auth:Address"] = _authUrl,
                ["ReverseProxy:Clusters:catalog-cluster:Destinations:catalog:Address"] = _catalogUrl
            };

            if (_paymentsUrl is not null)
            {
                overrides["ReverseProxy:Clusters:payments-cluster:Destinations:payments:Address"] = _paymentsUrl;
            }
            
            if (_graphragUrl is not null)
            {
                overrides["ReverseProxy:Clusters:graphrag-cluster:Destinations:graphrag:Address"] = _graphragUrl;
            }

            cfg.AddInMemoryCollection(overrides);
        });
    }
}