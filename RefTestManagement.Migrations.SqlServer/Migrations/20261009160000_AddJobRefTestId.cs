using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddJobRefTestId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RefTestId",
                table: "Jobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_RefTestId_Status",
                table: "Jobs",
                columns: new[] { "RefTestId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_RefTestId_Status",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "RefTestId",
                table: "Jobs");
        }
    }
}
