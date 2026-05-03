using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixProduct3DStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Material",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PrinterType",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "RelatedProductIds",
                table: "Products",
                newName: "SupportedMaterials");

            migrationBuilder.RenameColumn(
                name: "Attributes",
                table: "Products",
                newName: "CompatiblePrinters");

            migrationBuilder.AddColumn<double>(
                name: "AverageRating",
                table: "Products",
                type: "double precision",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "DownloadCount",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Settings_FilamentUsedGrams",
                table: "Products",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Settings_InfillPercentage",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Settings_LayerHeight",
                table: "Products",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Settings_NozzleSize",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Settings_PrintTimeMinutes",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Settings_SupportsRequired",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Size_DepthMm",
                table: "Products",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Size_HeightMm",
                table: "Products",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Size_WidthMm",
                table: "Products",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ProductAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductAttributes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelatedProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductRelations_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Status",
                table: "Products",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributes_ProductId",
                table: "ProductAttributes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributes_ProductId_Key",
                table: "ProductAttributes",
                columns: new[] { "ProductId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductRelations_ProductId",
                table: "ProductRelations",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRelations_ProductId_RelatedProductId_Type",
                table: "ProductRelations",
                columns: new[] { "ProductId", "RelatedProductId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductRelations_RelatedProductId",
                table: "ProductRelations",
                column: "RelatedProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductAttributes");

            migrationBuilder.DropTable(
                name: "ProductRelations");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Slug",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Status",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DownloadCount",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Settings_FilamentUsedGrams",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Settings_InfillPercentage",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Settings_LayerHeight",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Settings_NozzleSize",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Settings_PrintTimeMinutes",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Settings_SupportsRequired",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Size_DepthMm",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Size_HeightMm",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Size_WidthMm",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "SupportedMaterials",
                table: "Products",
                newName: "RelatedProductIds");

            migrationBuilder.RenameColumn(
                name: "CompatiblePrinters",
                table: "Products",
                newName: "Attributes");

            migrationBuilder.AddColumn<string>(
                name: "Material",
                table: "Products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrinterType",
                table: "Products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
