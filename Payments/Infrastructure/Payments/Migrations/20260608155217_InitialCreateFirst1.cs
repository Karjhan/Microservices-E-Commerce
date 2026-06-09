using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Payments.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateFirst1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecipientEmail",
                table: "Payments",
                type: "character varying(329)",
                maxLength: 329,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecipientEmail",
                table: "Payments");
        }
    }
}
