using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Handball.Belgium.RefTestManagement.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    SeqId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StreamId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ActorEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Headers = table.Column<string>(type: "text", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.SeqId);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobType = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecuteAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefTestTitles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefTestTitles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefTests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TitleId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LastName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Token = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SendInvitationsAutomatically = table.Column<bool>(type: "boolean", nullable: false),
                    InvitationSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NumberOfQuestions = table.Column<int>(type: "integer", nullable: false),
                    MaxTimeInMinutes = table.Column<int>(type: "integer", nullable: false),
                    QuestionIds = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CurrentQuestionIndex = table.Column<int>(type: "integer", nullable: true),
                    QuestionScore = table.Column<int>(type: "integer", nullable: true),
                    AnswerScore = table.Column<int>(type: "integer", nullable: true),
                    AnswerTotal = table.Column<int>(type: "integer", nullable: true),
                    Percentage = table.Column<double>(type: "double precision", nullable: true),
                    SelectedAnswerIds = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    WrongQuestionIds = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    WrongAnswerIds = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SendResultsAutomatically = table.Column<bool>(type: "boolean", nullable: false),
                    ResultsSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: true),
                    PrivacyNoticeVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PrivacyNoticeAcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsAnonymized = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AnonymizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, defaultValue: ""),
                    CreatorEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, defaultValue: ""),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
