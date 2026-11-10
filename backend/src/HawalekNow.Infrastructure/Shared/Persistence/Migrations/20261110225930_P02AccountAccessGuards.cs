using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HawalekNow.Infrastructure.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P02AccountAccessGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessStatus",
                schema: "identity",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_AccessStatus",
                schema: "identity",
                table: "Users",
                sql: "[AccessStatus] IN (1,2,3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_AccessStatus",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AccessStatus",
                schema: "identity",
                table: "Users");
        }
    }
}
