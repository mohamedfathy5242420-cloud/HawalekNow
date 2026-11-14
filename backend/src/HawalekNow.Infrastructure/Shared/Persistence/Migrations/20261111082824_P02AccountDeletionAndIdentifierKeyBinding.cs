using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HawalekNow.Infrastructure.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P02AccountDeletionAndIdentifierKeyBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                schema: "operations",
                table: "Outbox",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountDeletionAudit",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Generation = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDeletionAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountDeletions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Generation = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PurgeAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RestoredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScheduledJobId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDeletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountDeletions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdentifierKeyBinding",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Fingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentifierKeyBinding", x => x.Id);
                    table.CheckConstraint("CK_IdentifierKeyBinding_Singleton", "[Id] = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_SubjectId",
                schema: "operations",
                table: "Outbox",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletionAudit_UserId_Generation_Kind",
                schema: "identity",
                table: "AccountDeletionAudit",
                columns: new[] { "UserId", "Generation", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletions_PurgeAt",
                schema: "identity",
                table: "AccountDeletions",
                column: "PurgeAt");

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletions_UserId",
                schema: "identity",
                table: "AccountDeletions",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountDeletionAudit",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AccountDeletions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "IdentifierKeyBinding",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_Outbox_SubjectId",
                schema: "operations",
                table: "Outbox");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                schema: "operations",
                table: "Outbox");
        }
    }
}
