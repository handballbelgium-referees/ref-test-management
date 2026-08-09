using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertSentFieldsToDateTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new DateTime columns first
            migrationBuilder.AddColumn<DateTime>(
                name: "InvitationSentAt",
                table: "RefTests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResultsSentAt",
                table: "RefTests",
                type: "datetime2",
                nullable: true);

            // Migrate data: set timestamp to CreatedAt for all records where the bool was true
            migrationBuilder.Sql(@"
                UPDATE RefTests 
                SET InvitationSentAt = CreatedAt 
                WHERE InvitationSent = 1
            ");

            migrationBuilder.Sql(@"
                UPDATE RefTests 
                SET ResultsSentAt = CompletedAt 
                WHERE ResultsSent = 1 AND CompletedAt IS NOT NULL
            ");

            // Now drop the old boolean columns
            migrationBuilder.DropColumn(
                name: "InvitationSent",
                table: "RefTests");

            migrationBuilder.DropColumn(
                name: "ResultsSent",
                table: "RefTests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvitationSentAt",
                table: "RefTests");

            migrationBuilder.DropColumn(
                name: "ResultsSentAt",
                table: "RefTests");

            migrationBuilder.AddColumn<bool>(
                name: "InvitationSent",
                table: "RefTests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ResultsSent",
                table: "RefTests",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
