using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Migrations.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class AddExpirationJobDeduplicationKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeduplicationKey",
                table: "Jobs",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_DeduplicationKey",
                table: "Jobs",
                column: "DeduplicationKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_DeduplicationKey",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "DeduplicationKey",
                table: "Jobs");
        }
    }
}
