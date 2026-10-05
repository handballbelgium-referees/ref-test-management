using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Auth0.Services;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class ApprovalNotificationEmailJobHandlerTests
{
    [Fact]
    public async Task SendsNotificationsOnlyToApproversWithPermissionOnConfiguredAudience()
    {
        using var provider = Auth0ManagementTestServices.CreateProvider();
        var emailService = new RecordingEmailService();
        var handler = new ApprovalNotificationEmailJobHandler(
            provider.GetRequiredService<IAuth0ManagementService>(),
            emailService,
            new EmailConfiguration { BaseUrl = "https://ref-test.example" },
            NullLogger<ApprovalNotificationEmailJobHandler>.Instance);
        var payload = new ApprovalNotificationEmailPayload(
            "Test Creator",
            "creator@example.org",
            "Season",
            [new ApprovalNotificationRefTestItem(
                Guid.NewGuid(),
                "Ref",
                "Test",
                "participant@example.org",
                ScheduledAt: null)]);
        var job = Job.Create(JobType.ApprovalNotificationEmail, JsonSerializer.Serialize(payload));

        await handler.HandleAsync(job, TestContext.Current.CancellationToken);

        var expected = new[]
        {
            "direct-target-exact@example.org",
            "direct-target-superadmin@example.org",
            "direct-target-wildcard@example.org",
            "role-target-exact@example.org",
            "role-target-superadmin@example.org",
            "role-target-wildcard@example.org"
        };
        Assert.Equal(
            expected.OrderBy(email => email, StringComparer.Ordinal),
            emailService.Recipients.OrderBy(email => email, StringComparer.Ordinal));
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public List<string> Recipients { get; } = [];

        public Task SendApprovalNotificationAsync(
            string approverName,
            string approverEmail,
            string creatorName,
            string? titleValue,
            List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems,
            string baseUrl,
            CancellationToken cancellationToken)
        {
            Recipients.Add(approverEmail);
            return Task.CompletedTask;
        }

        public Task SendRefTestInvitationAsync(
            Guid refTestId,
            string name,
            string email,
            string token,
            int numberOfQuestions,
            int maxTimeInMinutes,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendRefTestResultsAsync(
            Guid refTestId,
            string name,
            string email,
            int questionScore,
            int answerScore,
            int totalQuestions,
            int answerTotal,
            double percentage,
            List<string> selectedAnswerIds,
            List<string> wrongQuestionIds,
            List<string> wrongAnswerIds,
            List<Question> questionsWithCorrectAnswers,
            bool scheduleEmail,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendReportEmailAsync(
            string recipientEmail,
            byte[] excelReport,
            byte[] pdfReport,
            string timestamp,
            int refTestCount,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendApprovalDecisionAsync(
            string creatorName,
            string creatorEmail,
            string approverName,
            bool isApproved,
            string? rejectionReason,
            string? titleValue,
            List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendPersonalDataExportVerificationAsync(
            string recipientEmail,
            string challengeKey,
            DateTime expiresAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendPrivacyWithdrawalVerificationAsync(
            string recipientEmail,
            string challengeKey,
            DateTime expiresAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> SendPersonalDataExportAsync(
            string recipientEmail,
            IReadOnlyList<EmailAttachment> attachments,
            Func<CancellationToken, Task<bool>> finalDeliverabilityCheck,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
