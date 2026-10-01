using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Migrations.SQLite.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalDataExportRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersonalDataExportRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Locale = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ProtectedDeliveryKey = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ChallengeEmailSentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastDeliveryAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeliveryAttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalDataExportRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalDataExportRequests_KeyHash",
                table: "PersonalDataExportRequests",
                column: "KeyHash");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalDataExportRequests_VerifiedAt_ExpiresAt",
                table: "PersonalDataExportRequests",
                columns: new[] { "VerifiedAt", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonalDataExportRequests");
        }
    }
}
