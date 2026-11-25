using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HawalekNow.Infrastructure.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P03CatalogFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "Activities",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameArKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, collation: "Latin1_General_100_BIN2"),
                    NameEnKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, collation: "Latin1_General_100_BIN2"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                    table.CheckConstraint("CK_Activities_Names", "LEN([NameAr]) > 0 AND LEN([NameEn]) > 0 AND LEN([NameArKey]) > 0 AND LEN([NameEnKey]) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameArKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, collation: "Latin1_General_100_BIN2"),
                    NameEnKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, collation: "Latin1_General_100_BIN2"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.UniqueConstraint("AK_Categories_Id_ActivityId", x => new { x.Id, x.ActivityId });
                    table.CheckConstraint("CK_Categories_Names", "LEN([NameAr]) > 0 AND LEN([NameEn]) > 0 AND LEN([NameArKey]) > 0 AND LEN([NameEnKey]) > 0");
                    table.CheckConstraint("CK_Categories_Parent", "[ParentId] IS NULL OR [ParentId] <> [Id]");
                    table.ForeignKey(
                        name: "FK_Categories_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalSchema: "catalog",
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_Categories_Categories_ParentId_ActivityId",
                        columns: x => new { x.ParentId, x.ActivityId },
                        principalSchema: "catalog",
                        principalTable: "Categories",
                        principalColumns: new[] { "Id", "ActivityId" },
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_NameArKey",
                schema: "catalog",
                table: "Activities",
                column: "NameArKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_NameEnKey",
                schema: "catalog",
                table: "Activities",
                column: "NameEnKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ActivityId_NameArKey",
                schema: "catalog",
                table: "Categories",
                columns: new[] { "ActivityId", "NameArKey" },
                unique: true,
                filter: "[ParentId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ActivityId_NameEnKey",
                schema: "catalog",
                table: "Categories",
                columns: new[] { "ActivityId", "NameEnKey" },
                unique: true,
                filter: "[ParentId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ActivityId_ParentId_NameArKey",
                schema: "catalog",
                table: "Categories",
                columns: new[] { "ActivityId", "ParentId", "NameArKey" },
                unique: true,
                filter: "[ParentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ActivityId_ParentId_NameEnKey",
                schema: "catalog",
                table: "Categories",
                columns: new[] { "ActivityId", "ParentId", "NameEnKey" },
                unique: true,
                filter: "[ParentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentId_ActivityId",
                schema: "catalog",
                table: "Categories",
                columns: new[] { "ParentId", "ActivityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Categories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Activities",
                schema: "catalog");
        }
    }
}
