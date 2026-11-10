using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HawalekNow.Infrastructure.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P02RegistrationAndIdentifierReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NormalizedUserName",
                schema: "identity",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                schema: "identity",
                table: "Users",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(254)",
                oldMaxLength: 254);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TermsAcceptedAt",
                schema: "identity",
                table: "Users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReservedIdentifiers",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountType = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Digest = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservedIdentifiers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservedIdentifiers_AccountType_Kind_Digest",
                schema: "identity",
                table: "ReservedIdentifiers",
                columns: new[] { "AccountType", "Kind", "Digest" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservedIdentifiers",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                schema: "identity",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedUserName",
                schema: "identity",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                schema: "identity",
                table: "Users",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(254)",
                oldMaxLength: 254,
                oldCollation: "Latin1_General_100_BIN2");
        }
    }
}
