using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacyNoticeAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PrivacyNoticeAcceptedAt",
                table: "RefTests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyNoticeVersion",
                table: "RefTests",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrivacyNoticeAcceptedAt",
                table: "RefTests");

            migrationBuilder.DropColumn(
                name: "PrivacyNoticeVersion",
                table: "RefTests");
        }
    }
}
