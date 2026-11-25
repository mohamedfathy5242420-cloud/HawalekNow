using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HawalekNow.Infrastructure.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P03SynonymGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "search");

            migrationBuilder.CreateTable(
                name: "SynonymGroups",
                schema: "search",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SynonymGroups", x => x.Id);
                    table.CheckConstraint("CK_SynonymGroups_Names", "LEN([NameAr]) > 0 AND LEN([NameEn]) > 0");
                });

            migrationBuilder.CreateTable(
                name: "SynonymTerms",
                schema: "search",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Original = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NormalizedKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SynonymTerms", x => x.Id);
                    table.CheckConstraint("CK_SynonymTerms_Text", "LEN([Original]) > 0 AND LEN([NormalizedKey]) > 0");
                    table.ForeignKey(
                        name: "FK_SynonymTerms_SynonymGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "search",
                        principalTable: "SynonymGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SynonymTerms_GroupId",
                schema: "search",
                table: "SynonymTerms",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SynonymTerms_NormalizedKey",
                schema: "search",
                table: "SynonymTerms",
                column: "NormalizedKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SynonymTerms",
                schema: "search");

            migrationBuilder.DropTable(
                name: "SynonymGroups",
                schema: "search");
        }
    }
}
