using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Migrations.PostgreSQL.Migrations
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Locale = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProtectedDeliveryKey = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChallengeEmailSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastDeliveryAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveryAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
