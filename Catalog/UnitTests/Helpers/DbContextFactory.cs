using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Helpers;

public static class DbContextFactory
{
    private static CatalogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite("Filename=:memory:")
            .Options;

        var context = new CatalogDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        return context;
    }
}