using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HawalekNow.Infrastructure.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P04StoresAndLogoStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stores",
                schema: "merchants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true),
                    WhatsApp = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true),
                    Facebook = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Instagram = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TikTok = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LogoKey = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                    table.CheckConstraint("CK_Stores_Contact", "NULLIF(LTRIM(RTRIM([Phone])), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM([WhatsApp])), '') IS NOT NULL");
                    table.CheckConstraint("CK_Stores_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.ForeignKey(
                        name: "FK_Stores_Accounts_MerchantAccountId",
                        column: x => x.MerchantAccountId,
                        principalSchema: "merchants",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stores_ActivityId",
                schema: "merchants",
                table: "Stores",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_LogoKey",
                schema: "merchants",
                table: "Stores",
                column: "LogoKey",
                unique: true,
                filter: "[LogoKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_MerchantAccountId",
                schema: "merchants",
                table: "Stores",
                column: "MerchantAccountId",
                unique: true);

            // Physical reference protection without a cross-module entity/navigation dependency.
            migrationBuilder.AddForeignKey(
                name: "FK_Stores_Activities_ActivityId",
                schema: "merchants", table: "Stores", column: "ActivityId",
                principalSchema: "catalog", principalTable: "Activities", principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stores",
                schema: "merchants");
        }
    }
}
