using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefTestTitles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefTestTitles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefTests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Token = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SendInvitationsAutomatically = table.Column<bool>(type: "bit", nullable: false),
                    InvitationSent = table.Column<bool>(type: "bit", nullable: false),
                    NumberOfQuestions = table.Column<int>(type: "int", nullable: false),
                    MaxTimeInMinutes = table.Column<int>(type: "int", nullable: false),
                    QuestionIds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CurrentQuestionIndex = table.Column<int>(type: "int", nullable: true),
                    QuestionScore = table.Column<int>(type: "int", nullable: true),
                    AnswerScore = table.Column<int>(type: "int", nullable: true),
                    AnswerTotal = table.Column<int>(type: "int", nullable: true),
                    Percentage = table.Column<double>(type: "float", nullable: true),
                    SelectedAnswerIds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    WrongQuestionIds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    WrongAnswerIds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SendResultsAutomatically = table.Column<bool>(type: "bit", nullable: false),
                    ResultsSent = table.Column<bool>(type: "bit", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefTests_RefTestTitles_TitleId",
                        column: x => x.TitleId,
                        principalTable: "RefTestTitles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefTests_CreatedAt",
                table: "RefTests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefTests_Email",
                table: "RefTests",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_RefTests_Status",
                table: "RefTests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RefTests_TitleId",
                table: "RefTests",
                column: "TitleId");

            migrationBuilder.CreateIndex(
                name: "IX_RefTests_Token",
                table: "RefTests",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefTests");

            migrationBuilder.DropTable(
                name: "RefTestTitles");
        }
    }
}
