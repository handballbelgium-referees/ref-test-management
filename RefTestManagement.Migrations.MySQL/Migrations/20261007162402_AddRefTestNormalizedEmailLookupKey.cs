using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Migrations.MySQL.Migrations
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
                type: "varbinary(32)",
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
