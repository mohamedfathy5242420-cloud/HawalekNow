using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HawalekNow.Infrastructure.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P03LocationsHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "locations");

            migrationBuilder.CreateTable(
                name: "Governorates",
                schema: "locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Governorates", x => x.Id);
                    table.CheckConstraint("CK_Governorates_Names", "LEN([NameAr]) > 0 AND LEN([NameEn]) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Cities",
                schema: "locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GovernorateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.Id);
                    table.UniqueConstraint("AK_Cities_Id_GovernorateId", x => new { x.Id, x.GovernorateId });
                    table.CheckConstraint("CK_Cities_Names", "LEN([NameAr]) > 0 AND LEN([NameEn]) > 0");
                    table.ForeignKey(
                        name: "FK_Cities_Governorates_GovernorateId",
                        column: x => x.GovernorateId,
                        principalSchema: "locations",
                        principalTable: "Governorates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Areas",
                schema: "locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GovernorateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Areas", x => x.Id);
                    table.CheckConstraint("CK_Areas_Names", "LEN([NameAr]) > 0 AND LEN([NameEn]) > 0");
                    table.ForeignKey(
                        name: "FK_Areas_Cities_CityId_GovernorateId",
                        columns: x => new { x.CityId, x.GovernorateId },
                        principalSchema: "locations",
                        principalTable: "Cities",
                        principalColumns: new[] { "Id", "GovernorateId" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Areas_CityId_GovernorateId",
                schema: "locations",
                table: "Areas",
                columns: new[] { "CityId", "GovernorateId" });

            migrationBuilder.CreateIndex(
                name: "IX_Areas_GovernorateId_CityId_Id",
                schema: "locations",
                table: "Areas",
                columns: new[] { "GovernorateId", "CityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Cities_GovernorateId_Id",
                schema: "locations",
                table: "Cities",
                columns: new[] { "GovernorateId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Areas",
                schema: "locations");

            migrationBuilder.DropTable(
                name: "Cities",
                schema: "locations");

            migrationBuilder.DropTable(
                name: "Governorates",
                schema: "locations");
        }
    }
}
