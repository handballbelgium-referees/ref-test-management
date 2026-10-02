using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Migrations.SQLite.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacyWithdrawalPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrivacyWithdrawalBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TargetCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacyWithdrawalBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrivacyWithdrawalChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    NormalizedEmailHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_PrivacyWithdrawalChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrivacyWithdrawalBatchTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    BatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RefTestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ErasureStartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacyWithdrawalBatchTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrivacyWithdrawalBatchTargets_PrivacyWithdrawalBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "PrivacyWithdrawalBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyWithdrawalBatches_CompletedAt",
                table: "PrivacyWithdrawalBatches",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyWithdrawalBatchTargets_BatchId_CompletedAt",
                table: "PrivacyWithdrawalBatchTargets",
                columns: new[] { "BatchId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyWithdrawalBatchTargets_BatchId_RefTestId",
                table: "PrivacyWithdrawalBatchTargets",
                columns: new[] { "BatchId", "RefTestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyWithdrawalChallenges_NormalizedEmailHash",
                table: "PrivacyWithdrawalChallenges",
                column: "NormalizedEmailHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyWithdrawalChallenges_KeyHash",
                table: "PrivacyWithdrawalChallenges",
                column: "KeyHash");

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyWithdrawalChallenges_VerifiedAt_ExpiresAt",
                table: "PrivacyWithdrawalChallenges",
                columns: new[] { "VerifiedAt", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrivacyWithdrawalBatchTargets");

            migrationBuilder.DropTable(
                name: "PrivacyWithdrawalChallenges");

            migrationBuilder.DropTable(
                name: "PrivacyWithdrawalBatches");
        }
    }
}
