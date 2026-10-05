using Handball.Belgium.RefTestManagement.Application.Models;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public class ApprovalDecisionEmailPayloadTests
{
    [Fact]
    public void RedactRejectionReasonFor_ClearsReasonWhenGroupedPayloadContainsRefTest()
    {
        var refTestId = Guid.NewGuid();
        var otherRefTestId = Guid.NewGuid();
        var payload = NewPayload("free-text reason", refTestId, otherRefTestId);

        var redacted = payload.RedactRejectionReasonFor(refTestId);

        Assert.NotSame(payload, redacted);
        Assert.Null(redacted.RejectionReason);
        Assert.Equal(payload.RefTests, redacted.RefTests);
        Assert.Equal(payload.CreatorEmail, redacted.CreatorEmail);
    }

    [Fact]
    public void RedactRejectionReasonFor_LeavesUnrelatedPayloadUnchanged()
    {
        var payload = NewPayload("free-text reason", Guid.NewGuid());

        var redacted = payload.RedactRejectionReasonFor(Guid.NewGuid());

        Assert.Same(payload, redacted);
        Assert.Equal("free-text reason", redacted.RejectionReason);
    }

    private static ApprovalDecisionEmailPayload NewPayload(string reason, params Guid[] refTestIds) =>
        new(
            "Creator",
            "creator@example.org",
            "Approver",
            IsApproved: false,
            RejectionReason: reason,
            TitleValue: null,
            refTestIds.Select(id => new ApprovalNotificationRefTestItem(
                id,
                "First",
                "Last",
                "participant@example.org",
                ScheduledAt: null)).ToList());
}
