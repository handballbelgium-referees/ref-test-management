using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacyWithdrawalRetryRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "PrivacyWithdrawalBatchTargets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FailureCode",
                table: "PrivacyWithdrawalBatchTargets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "PrivacyWithdrawalBatchTargets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetryExhaustedAt",
                table: "PrivacyWithdrawalBatchTargets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LatestJobId",
                table: "PrivacyWithdrawalBatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrivacyWithdrawalBatchId",
                table: "Jobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_PrivacyWithdrawalBatchId",
                table: "Jobs",
                column: "PrivacyWithdrawalBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_PrivacyWithdrawalBatchId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "PrivacyWithdrawalBatchTargets");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                table: "PrivacyWithdrawalBatchTargets");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "PrivacyWithdrawalBatchTargets");

            migrationBuilder.DropColumn(
                name: "RetryExhaustedAt",
                table: "PrivacyWithdrawalBatchTargets");

            migrationBuilder.DropColumn(
                name: "LatestJobId",
                table: "PrivacyWithdrawalBatches");

            migrationBuilder.DropColumn(
                name: "PrivacyWithdrawalBatchId",
                table: "Jobs");
        }
    }
}
