using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace IntegrationTests.Migrations;

public class ReferenceDataMigrationTests : IAsyncLifetime
{
    private const string BeforeData = "20261127125257_AddReferenceData";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("catalog_migration_test")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private CatalogDbContext CreateContext() => new(
        new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    private static async Task InsertLegacyProductAsync(
        CatalogDbContext db, Guid id, Guid categoryId, string materials, DateTime createdAt)
    {
        var columns = await db.Database.SqlQueryRaw<ColumnInfo>(
            """
            SELECT column_name AS "Name", data_type AS "Type"
            FROM information_schema.columns
            WHERE table_name = 'Products' AND is_nullable = 'NO' AND column_default IS NULL
            """).ToListAsync();

        var known = new Dictionary<string, string>
        {
            ["Id"] = $"'{id}'",
            ["Name"] = $"'Legacy {id}'",
            ["Slug"] = $"'legacy-{id}'",
            ["CategoryId"] = $"'{categoryId}'",
            ["SupportedMaterials"] = $"'{materials}'",
            ["CreatedAt"] = $"'{createdAt:O}'"
        };

        string ValueFor(ColumnInfo column) => known.TryGetValue(column.Name, out var value)
            ? value
            : column.Type switch
            {
                "uuid" => "gen_random_uuid()",
                "boolean" => "false",
                "integer" or "numeric" or "double precision" or "bigint" => "0",
                var t when t.StartsWith("timestamp") => "now()",
                _ => "''"
            };

        var names = string.Join(", ", columns.Select(c => $"\"{c.Name}\""));
        var values = string.Join(", ", columns.Select(ValueFor));

        await db.Database.ExecuteSqlRawAsync($"INSERT INTO \"Products\" ({names}) VALUES ({values})");
    }

    private static async Task<List<string>> QueryAsync(CatalogDbContext db, string sql) =>
        await db.Database.SqlQueryRaw<string>(sql).ToListAsync();

    [Fact]
    public async Task Existing_Materials_And_Categories_Survive_The_Migration_And_Can_Be_Rolled_Back()
    {
        await using var db = CreateContext();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(BeforeData);

        var categoryA = Guid.NewGuid();
        var categoryB = Guid.NewGuid();
        var categoryC = Guid.NewGuid();

        var both = Guid.NewGuid();
        var resin = Guid.NewGuid();
        var none = Guid.NewGuid();
        var duplicated = Guid.NewGuid();

        var start = DateTime.UtcNow.AddDays(-10);
        await InsertLegacyProductAsync(db, both, categoryA, "PLA,PETG", start);
        await InsertLegacyProductAsync(db, resin, categoryA, "Resin", start.AddDays(1));
        await InsertLegacyProductAsync(db, none, categoryB, "", start.AddDays(2));
        await InsertLegacyProductAsync(db, duplicated, categoryC, "ABS,PLA,PLA", start.AddDays(3));

        await migrator.MigrateAsync();

        (await QueryAsync(db, """SELECT "Code" AS "Value" FROM "FilamentTypes" ORDER BY "Code" """))
            .Should().Equal("ABS", "Nylon", "PETG", "PLA", "Resin", "TPU");

        async Task<List<string>> CodesOf(Guid productId) => await QueryAsync(db, $"""
            SELECT f."Code" AS "Value" FROM "ProductFilamentTypes" pf
            JOIN "FilamentTypes" f ON f."Id" = pf."FilamentTypeId"
            WHERE pf."ProductId" = '{productId}' ORDER BY f."Code"
            """);

        (await CodesOf(both)).Should().Equal("PETG", "PLA");
        (await CodesOf(resin)).Should().Equal("Resin");
        (await CodesOf(none)).Should().BeEmpty();
        (await CodesOf(duplicated)).Should().Equal("ABS", "PLA");

        (await QueryAsync(db, """SELECT "Id"::text AS "Value" FROM "Categories" """))
            .Should().BeEquivalentTo([categoryA.ToString(), categoryB.ToString(), categoryC.ToString()]);

        (await QueryAsync(db, """SELECT "Name" AS "Value" FROM "Categories" ORDER BY "Name" """))
            .Should().Equal("Uncategorized", "Uncategorized 2", "Uncategorized 3");

        (await QueryAsync(db, $"""SELECT "CategoryId"::text AS "Value" FROM "Products" WHERE "Id" = '{resin}'"""))
            .Should().Equal(categoryA.ToString());

        (await QueryAsync(db, """
            SELECT column_name AS "Value" FROM information_schema.columns
            WHERE table_name = 'Products' AND column_name = 'SupportedMaterials'
            """)).Should().BeEmpty();

        var deleteInUse = () => db.Database.ExecuteSqlRawAsync($"""DELETE FROM "Categories" WHERE "Id" = '{categoryA}'""");
        await deleteInUse.Should().ThrowAsync<Exception>();

        await migrator.MigrateAsync(BeforeData);

        (await QueryAsync(db, $"""SELECT "SupportedMaterials" AS "Value" FROM "Products" WHERE "Id" = '{both}'"""))
            .Should().Equal("PETG,PLA");
        (await QueryAsync(db, $"""SELECT "SupportedMaterials" AS "Value" FROM "Products" WHERE "Id" = '{none}'"""))
            .Should().Equal("");
    }

    [Fact]
    public async Task An_Empty_Store_Migrates_Cleanly()
    {
        await using var db = CreateContext();

        await db.Database.MigrateAsync();

        (await QueryAsync(db, """SELECT "Code" AS "Value" FROM "FilamentTypes" """)).Should().HaveCount(6);
        (await QueryAsync(db, """SELECT "Name" AS "Value" FROM "Categories" """)).Should().BeEmpty();
    }

    private sealed record ColumnInfo(string Name, string Type);
}
