using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEventRedactedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RedactedAt",
                table: "AuditEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Timestamp_RedactedAt",
                table: "AuditEvents",
                columns: new[] { "Timestamp", "RedactedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_Timestamp_RedactedAt",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "RedactedAt",
                table: "AuditEvents");
        }
    }
}
