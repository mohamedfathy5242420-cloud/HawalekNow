using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HawalekNow.Infrastructure.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P03ReferenceSeedKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeedKeys",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeedKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeedKeys",
                schema: "locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceCommit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeedKeys", x => x.Id);
                    table.CheckConstraint("CK_LocationSeedKeys_Kind", "[Kind] IN (1,2,3)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeedKeys_SourceKey",
                schema: "catalog",
                table: "SeedKeys",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeedKeys_SourceKey",
                schema: "locations",
                table: "SeedKeys",
                column: "SourceKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeedKeys",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "SeedKeys",
                schema: "locations");
        }
    }
}
