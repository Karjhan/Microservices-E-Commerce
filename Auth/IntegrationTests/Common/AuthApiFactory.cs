using Domain.Abstractions.Messaging;
using Infrastructure.Messaging;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.Common;

public class AuthApiFactory : WebApplicationFactory<Program>
{
    public IServiceProvider Services => Server.Services;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var connString = PostgresContainer.Instance.GetConnectionString();

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connString
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services
                .SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AuthDbContext>));

            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<AuthDbContext>(options =>
            {
                options.UseNpgsql(PostgresContainer.Instance.GetConnectionString());
            });
        });
    }
}