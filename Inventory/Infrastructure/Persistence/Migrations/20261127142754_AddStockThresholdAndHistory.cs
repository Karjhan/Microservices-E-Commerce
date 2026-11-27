using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockThresholdAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_ProductId_OccurredAt",
                table: "InventoryMovements");

            migrationBuilder.AddColumn<string>(
                name: "ActorEmail",
                table: "InventoryMovements",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActorId",
                table: "InventoryMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LowStockThreshold",
                table: "InventoryItems",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ProductId_OccurredAt",
                table: "InventoryMovements",
                columns: new[] { "ProductId", "OccurredAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_ProductId_OccurredAt",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "ActorEmail",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "LowStockThreshold",
                table: "InventoryItems");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ProductId_OccurredAt",
                table: "InventoryMovements",
                columns: new[] { "ProductId", "OccurredAt" });
        }
    }
}
