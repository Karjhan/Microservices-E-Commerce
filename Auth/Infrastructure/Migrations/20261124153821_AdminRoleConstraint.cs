using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdminRoleConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FR-003: legacy staff roles become Customer before the constraint is applied.
            migrationBuilder.Sql("UPDATE \"Users\" SET \"Role\" = 'Customer' WHERE \"Role\" NOT IN ('Admin', 'Customer');");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Role",
                table: "Users",
                sql: "\"Role\" IN ('Admin', 'Customer')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Role",
                table: "Users");
        }
    }
}
