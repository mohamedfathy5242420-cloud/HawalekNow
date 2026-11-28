using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HawalekNow.Infrastructure.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P04BranchWeeklyHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Hours",
                schema: "merchants",
                table: "Branches",
                type: "nvarchar(max)",
                maxLength: 8192,
                nullable: true);

            // Draft S04 places have no recorded hours. Preserve them conservatively as explicitly closed.
            // No permanent default: every later insert must supply its own validated weekly schedule.
            migrationBuilder.Sql("""
                UPDATE [merchants].[Branches] SET [Hours] = N'[{"Day":0,"Closed":true,"Periods":[]},{"Day":1,"Closed":true,"Periods":[]},{"Day":2,"Closed":true,"Periods":[]},{"Day":3,"Closed":true,"Periods":[]},{"Day":4,"Closed":true,"Periods":[]},{"Day":5,"Closed":true,"Periods":[]},{"Day":6,"Closed":true,"Periods":[]}]';
                """);

            migrationBuilder.AlterColumn<string>(name: "Hours", schema: "merchants", table: "Branches",
                type: "nvarchar(max)", maxLength: 8192, nullable: false,
                oldClrType: typeof(string), oldType: "nvarchar(max)", oldMaxLength: 8192, oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Branches_Hours",
                schema: "merchants",
                table: "Branches",
                sql: "ISJSON([Hours]) = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Branches_Hours",
                schema: "merchants",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Hours",
                schema: "merchants",
                table: "Branches");
        }
    }
}
