using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HawalekNow.Infrastructure.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P04StoreModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                schema: "merchants",
                table: "Stores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                schema: "merchants",
                table: "Stores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                schema: "merchants",
                table: "Stores",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisibilityReason",
                schema: "merchants",
                table: "Stores",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stores_ModerationReasons",
                schema: "merchants",
                table: "Stores",
                sql: "([IsHidden]=0 OR NULLIF(LTRIM(RTRIM([VisibilityReason])), '') IS NOT NULL) AND ([IsSuspended]=0 OR NULLIF(LTRIM(RTRIM([SuspensionReason])), '') IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stores_ModerationReasons",
                schema: "merchants",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                schema: "merchants",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "IsSuspended",
                schema: "merchants",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "SuspensionReason",
                schema: "merchants",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "VisibilityReason",
                schema: "merchants",
                table: "Stores");
        }
    }
}
