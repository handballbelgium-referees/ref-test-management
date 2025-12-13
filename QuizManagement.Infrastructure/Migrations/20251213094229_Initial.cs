using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuizTitles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizTitles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuizSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Token = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvitationSent = table.Column<bool>(type: "bit", nullable: false),
                    NumberOfQuestions = table.Column<int>(type: "int", nullable: false),
                    MaxTimeInMinutes = table.Column<int>(type: "int", nullable: false),
                    QuestionIds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true),
                    TotalQuestions = table.Column<int>(type: "int", nullable: true),
                    Percentage = table.Column<double>(type: "float", nullable: true),
                    WrongQuestionIds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    WrongAnswerIds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizSessions_QuizTitles_TitleId",
                        column: x => x.TitleId,
                        principalTable: "QuizTitles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuizSessions_CreatedAt",
                table: "QuizSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_QuizSessions_Email",
                table: "QuizSessions",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_QuizSessions_Status",
                table: "QuizSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_QuizSessions_TitleId",
                table: "QuizSessions",
                column: "TitleId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizSessions_Token",
                table: "QuizSessions",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuizSessions");

            migrationBuilder.DropTable(
                name: "QuizTitles");
        }
    }
}
