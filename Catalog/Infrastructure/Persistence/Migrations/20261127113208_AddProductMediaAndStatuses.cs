using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductMediaAndStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "ProductImages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "ProductImages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ProductImages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill existing images (data-model.md, product_images):
            // - sort order follows the current gallery, primary image first;
            // - content type comes from the stored file extension (new uploads use detected types);
            // - exactly one primary image per product, as the partial unique index requires.
            // Product statuses need no mapping: the enum already holds the five values, stored as integers.
            migrationBuilder.Sql("""
                UPDATE "ProductImages" AS i
                SET "SortOrder" = r.rn
                FROM (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "ProductId" ORDER BY "IsPrimary" DESC, "Id") - 1 AS rn
                    FROM "ProductImages"
                ) AS r
                WHERE i."Id" = r."Id";
                """);

            migrationBuilder.Sql("""
                UPDATE "ProductImages"
                SET "ContentType" = CASE
                    WHEN lower("ObjectKey") LIKE '%.png' THEN 'image/png'
                    WHEN lower("ObjectKey") LIKE '%.webp' THEN 'image/webp'
                    ELSE 'image/jpeg'
                END;
                """);

            migrationBuilder.Sql("""
                UPDATE "ProductImages" SET "IsPrimary" = ("SortOrder" = 0);
                """);

            migrationBuilder.CreateTable(
                name: "ProductModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectKey = table.Column<string>(type: "text", nullable: false),
                    OriginalFilename = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsPreviewable = table.Column<bool>(type: "boolean", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductModels", x => x.Id);
                    table.CheckConstraint("CK_ProductModels_Format", "\"Format\" IN ('glb','gltf','stl','3mf','obj')");
                    table.CheckConstraint("CK_ProductModels_Size", "\"SizeBytes\" <= 104857600");
                    table.ForeignKey(
                        name: "FK_ProductModels_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId_SortOrder",
                table: "ProductImages",
                columns: new[] { "ProductId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_ProductImages_Primary",
                table: "ProductImages",
                column: "ProductId",
                unique: true,
                filter: "\"IsPrimary\"");

            migrationBuilder.CreateIndex(
                name: "IX_ProductModels_ProductId",
                table: "ProductModels",
                column: "ProductId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductModels");

            migrationBuilder.DropIndex(
                name: "IX_ProductImages_ProductId_SortOrder",
                table: "ProductImages");

            migrationBuilder.DropIndex(
                name: "UX_ProductImages_Primary",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "ProductImages");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");
        }
    }
}
