using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HawalekNow.Infrastructure.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P04StoreActivityChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityChanges",
                schema: "merchants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityChanges", x => x.Id);
                    table.CheckConstraint("CK_ActivityChanges_Different", "[PreviousActivityId] <> [RequestedActivityId]");
                    table.CheckConstraint("CK_ActivityChanges_Review", "([Status]=1 AND [ReviewedBy] IS NULL AND [ReviewedAt] IS NULL AND [ReviewReason] IS NULL) OR ([Status] IN (2,3) AND [ReviewedBy] IS NOT NULL AND [ReviewedAt] IS NOT NULL AND ([Status]=2 OR NULLIF(LTRIM(RTRIM([ReviewReason])), '') IS NOT NULL))");
                    table.CheckConstraint("CK_ActivityChanges_Status", "[Status] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_ActivityChanges_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "merchants",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityChanges_PreviousActivityId",
                schema: "merchants",
                table: "ActivityChanges",
                column: "PreviousActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityChanges_RequestedActivityId",
                schema: "merchants",
                table: "ActivityChanges",
                column: "RequestedActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityChanges_Status_RequestedAt_Id",
                schema: "merchants",
                table: "ActivityChanges",
                columns: new[] { "Status", "RequestedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityChanges_StoreId",
                schema: "merchants",
                table: "ActivityChanges",
                column: "StoreId",
                unique: true,
                filter: "[Status] = 1");

            // Primitive Catalog integrity without cross-module entity/navigation references.
            migrationBuilder.AddForeignKey(name: "FK_ActivityChanges_Activities_PreviousActivityId", schema: "merchants", table: "ActivityChanges",
                column: "PreviousActivityId", principalSchema: "catalog", principalTable: "Activities", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_ActivityChanges_Activities_RequestedActivityId", schema: "merchants", table: "ActivityChanges",
                column: "RequestedActivityId", principalSchema: "catalog", principalTable: "Activities", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityChanges",
                schema: "merchants");
        }
    }
}
