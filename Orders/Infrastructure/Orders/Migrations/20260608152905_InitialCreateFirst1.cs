using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Orders.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateFirst1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecipientEmail",
                table: "Orders",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecipientEmail",
                table: "Orders");
        }
    }
}
