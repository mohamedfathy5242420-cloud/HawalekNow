using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace HawalekNow.Infrastructure.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P04BranchesAndAdministrativeReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Branches",
                schema: "merchants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    GovernorateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<Point>(type: "geography", nullable: false),
                    Phone = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true),
                    WhatsApp = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true),
                    PublicFixedAddressConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                    table.CheckConstraint("CK_Branches_Kind", "[Kind] IN (1,2)");
                    table.CheckConstraint("CK_Branches_Position", "[Position].STSrid = 4326 AND [Position].STGeometryType() = 'Point' AND [Position].STIsEmpty() = 0");
                    table.CheckConstraint("CK_Branches_PublicAddress", "[PublicFixedAddressConfirmed] = 1 AND LEN(LTRIM(RTRIM([Name]))) > 0 AND LEN(LTRIM(RTRIM([Address]))) > 0");
                    table.ForeignKey(
                        name: "FK_Branches_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "merchants",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Areas_Id_CityId_GovernorateId",
                schema: "locations",
                table: "Areas",
                columns: new[] { "Id", "CityId", "GovernorateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Branches_AreaId_CityId_GovernorateId",
                schema: "merchants",
                table: "Branches",
                columns: new[] { "AreaId", "CityId", "GovernorateId" });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_StoreId_Id",
                schema: "merchants",
                table: "Branches",
                columns: new[] { "StoreId", "Id" });

            // Cross-module integrity is primitive global DDL; the Area-owned unique index remains in Locations.
            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Areas_AdministrativePath", schema: "merchants", table: "Branches",
                columns: new[] { "AreaId", "CityId", "GovernorateId" }, principalSchema: "locations", principalTable: "Areas",
                principalColumns: new[] { "Id", "CityId", "GovernorateId" }, onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Branches",
                schema: "merchants");

            migrationBuilder.DropIndex(
                name: "IX_Areas_Id_CityId_GovernorateId",
                schema: "locations",
                table: "Areas");
        }
    }
}
