using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

public class PdfWorkerDbContextFactory : IDesignTimeDbContextFactory<PdfWorkerDbContext>
{
    public PdfWorkerDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(basePath, "appsettings.json")))
        {
            var hostDir = Path.Combine(basePath, "PdfWorker");
            if (File.Exists(Path.Combine(hostDir, "appsettings.json")))
                basePath = hostDir;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration["Database:ConnectionString"]
                               ?? throw new InvalidOperationException(
                                   "No connection string. Set Database:ConnectionString in appsettings or environment variables.");

        var optionsBuilder = new DbContextOptionsBuilder<PdfWorkerDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new PdfWorkerDbContext(optionsBuilder.Options);
    }
}