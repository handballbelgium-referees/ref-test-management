using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddRefTestSweepIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RefTests_Status_IsAnonymized_CompletedAt",
                table: "RefTests",
                columns: new[] { "Status", "IsAnonymized", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RefTests_Status_IsAnonymized_CreatedAt",
                table: "RefTests",
                columns: new[] { "Status", "IsAnonymized", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RefTests_Status_IsAnonymized_ExpiredAt",
                table: "RefTests",
                columns: new[] { "Status", "IsAnonymized", "ExpiredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RefTests_Status_IsAnonymized_StartedAt",
                table: "RefTests",
                columns: new[] { "Status", "IsAnonymized", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefTests_Status_IsAnonymized_CompletedAt",
                table: "RefTests");

            migrationBuilder.DropIndex(
                name: "IX_RefTests_Status_IsAnonymized_CreatedAt",
                table: "RefTests");

            migrationBuilder.DropIndex(
                name: "IX_RefTests_Status_IsAnonymized_ExpiredAt",
                table: "RefTests");

            migrationBuilder.DropIndex(
                name: "IX_RefTests_Status_IsAnonymized_StartedAt",
                table: "RefTests");
        }
    }
}
