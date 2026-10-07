using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddRefTestNormalizedEmailLookupKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "EmailLookupKey",
                table: "RefTests",
                type: "bytea",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefTests_EmailLookupKey",
                table: "RefTests",
                column: "EmailLookupKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefTests_EmailLookupKey",
                table: "RefTests");

            migrationBuilder.DropColumn(
                name: "EmailLookupKey",
                table: "RefTests");
        }
    }
}
