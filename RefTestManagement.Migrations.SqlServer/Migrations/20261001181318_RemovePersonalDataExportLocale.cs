using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemovePersonalDataExportLocale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Locale",
                table: "PersonalDataExportRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Locale",
                table: "PersonalDataExportRequests",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "en");
        }
    }
}
