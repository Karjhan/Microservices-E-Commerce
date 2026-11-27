using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MigrateProductReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data first, while the old materials column and the unlinked category ids still exist (research R13).

            // One filament type per material the store already knew; the code equals the old enum name, so links,
            // filters and events that carry the name keep working.
            migrationBuilder.Sql("""
                INSERT INTO "FilamentTypes"
                    ("Id", "Name", "NormalizedName", "Code", "DiameterMm", "ColorOptions", "IsActive", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), m.name, lower(m.name), m.name, NULL, '[]'::jsonb, TRUE, now(), now()
                FROM (VALUES ('PLA'), ('ABS'), ('PETG'), ('Resin'), ('Nylon'), ('TPU')) AS m(name)
                WHERE NOT EXISTS (SELECT 1 FROM "FilamentTypes" f WHERE f."Code" = m.name);
                """);

            // Copy each product's comma-separated materials into the join table.
            migrationBuilder.Sql("""
                INSERT INTO "ProductFilamentTypes" ("ProductId", "FilamentTypeId")
                SELECT DISTINCT p."Id", f."Id"
                FROM "Products" p
                CROSS JOIN LATERAL unnest(string_to_array(p."SupportedMaterials", ',')) AS m(code)
                JOIN "FilamentTypes" f ON f."Code" = btrim(m.code)
                WHERE p."SupportedMaterials" <> '';
                """);

            // Products pointed at bare category ids. Each distinct id becomes a category that keeps that id, so no
            // product has to be repointed. Names are numbered to stay unique and are meant to be renamed by an admin.
            migrationBuilder.Sql("""
                INSERT INTO "Categories" ("Id", "Name", "NormalizedName", "IsActive", "CreatedAt", "UpdatedAt")
                SELECT c."CategoryId",
                       CASE WHEN c.rn = 1 THEN 'Uncategorized' ELSE 'Uncategorized ' || c.rn END,
                       CASE WHEN c.rn = 1 THEN 'uncategorized' ELSE 'uncategorized ' || c.rn END,
                       TRUE, now(), now()
                FROM (
                    SELECT "CategoryId", row_number() OVER (ORDER BY min("CreatedAt"), "CategoryId") AS rn
                    FROM "Products"
                    GROUP BY "CategoryId"
                ) c
                WHERE NOT EXISTS (SELECT 1 FROM "Categories" x WHERE x."Id" = c."CategoryId");
                """);

            migrationBuilder.DropColumn(
                name: "SupportedMaterials",
                table: "Products");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "SupportedMaterials",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Put back the materials the old enum could hold. Other filament types have no enum value and are dropped
            // from the column; the categories and filament types themselves are removed with their tables.
            migrationBuilder.Sql("""
                UPDATE "Products" p
                SET "SupportedMaterials" = COALESCE((
                    SELECT string_agg(f."Code", ',' ORDER BY f."Code")
                    FROM "ProductFilamentTypes" pf
                    JOIN "FilamentTypes" f ON f."Id" = pf."FilamentTypeId"
                    WHERE pf."ProductId" = p."Id"
                      AND f."Code" IN ('PLA', 'ABS', 'PETG', 'Resin', 'Nylon', 'TPU')
                ), '');
                """);
        }
    }
}
