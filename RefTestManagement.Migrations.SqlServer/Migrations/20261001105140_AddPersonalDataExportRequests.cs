using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Infrastructure.Migrations
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProtectedDeliveryKey = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChallengeEmailSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastDeliveryAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveryAttemptCount = table.Column<int>(type: "int", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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
