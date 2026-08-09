using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Migrations.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    SeqId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    StreamId = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Data = table.Column<string>(type: "longtext", nullable: true),
                    Type = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActorName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    ActorEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Headers = table.Column<string>(type: "longtext", nullable: true),
                    IsArchived = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.SeqId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    JobType = table.Column<string>(type: "longtext", nullable: false),
                    Payload = table.Column<string>(type: "longtext", nullable: false),
                    Status = table.Column<string>(type: "varchar(255)", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExecuteAfter = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RefTestTitles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Value = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefTestTitles", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RefTests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TitleId = table.Column<Guid>(type: "char(36)", nullable: false),
                    FirstName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    LastName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Token = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    SendInvitationsAutomatically = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    InvitationSentAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    NumberOfQuestions = table.Column<int>(type: "int", nullable: false),
                    MaxTimeInMinutes = table.Column<int>(type: "int", nullable: false),
                    QuestionIds = table.Column<string>(type: "longtext", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "varchar(255)", nullable: false),
                    CurrentQuestionIndex = table.Column<int>(type: "int", nullable: true),
                    QuestionScore = table.Column<int>(type: "int", nullable: true),
                    AnswerScore = table.Column<int>(type: "int", nullable: true),
                    AnswerTotal = table.Column<int>(type: "int", nullable: true),
                    Percentage = table.Column<double>(type: "double", nullable: true),
                    SelectedAnswerIds = table.Column<string>(type: "longtext", maxLength: 4000, nullable: false),
                    WrongQuestionIds = table.Column<string>(type: "longtext", maxLength: 4000, nullable: false),
                    WrongAnswerIds = table.Column<string>(type: "longtext", maxLength: 4000, nullable: false),
                    SendResultsAutomatically = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ResultsSentAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Language = table.Column<string>(type: "longtext", nullable: true),
                    PrivacyNoticeVersion = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                    PrivacyNoticeAcceptedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RejectionReason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    IsAnonymized = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    AnonymizedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatorName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false, defaultValue: ""),
                    CreatorEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false, defaultValue: ""),
                    ScheduledAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_IsArchived",
                table: "AuditEvents",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_StreamId",
                table: "AuditEvents",
                column: "StreamId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_StreamId_Version",
                table: "AuditEvents",
                columns: new[] { "StreamId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Timestamp",
                table: "AuditEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Type",
                table: "AuditEvents",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CreatedAt",
                table: "Jobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status_ExecuteAfter_LockedUntil",
                table: "Jobs",
                columns: new[] { "Status", "ExecuteAfter", "LockedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_RefTests_CreatedAt",
                table: "RefTests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefTests_Email",
                table: "RefTests",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_RefTests_IsAnonymized",
                table: "RefTests",
                column: "IsAnonymized");

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
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "RefTests");

            migrationBuilder.DropTable(
                name: "RefTestTitles");
        }
    }
}
