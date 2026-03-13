using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalAndUserName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "RefTests",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "RefTests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("UPDATE RefTests SET ApprovedAt = CreatedAt");

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserEmail",
                table: "RefTests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "RefTests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefTests_ApprovalStatus",
                table: "RefTests",
                column: "ApprovalStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefTests_ApprovalStatus",
                table: "RefTests");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "RefTests");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "RefTests");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserEmail",
                table: "RefTests");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "RefTests");
        }
    }
}
