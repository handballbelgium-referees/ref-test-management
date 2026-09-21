using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Covers the anonymization step of the erasure path. Two properties matter and pull in opposite
/// directions: nothing identifying may survive, and the consent evidence GDPR Art. 7(1) requires
/// the controller to keep must survive.
/// </summary>
public class RefTestAnonymizationTests
{
    private static RefTest NewRefTest() =>
        RefTest.Create(
            titleId: Guid.NewGuid(),
            firstName: "John",
            lastName: "Doe",
            email: "john.doe@example.com",
            numberOfQuestions: 20,
            maxTimeInMinutes: 30,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: false);

    [Fact]
    public void Anonymize_RemovesNameAndEmail()
    {
        var refTest = NewRefTest();

        refTest.Anonymize();

        Assert.True(refTest.IsAnonymized);
        Assert.NotNull(refTest.AnonymizedAt);
        Assert.DoesNotContain("John", refTest.FirstName, StringComparison.Ordinal);
        Assert.DoesNotContain("Doe", refTest.LastName, StringComparison.Ordinal);
        Assert.DoesNotContain("john.doe@example.com", refTest.Email, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Anonymize_ReplacesTheTokenWithAUniquePlaceholder()
    {
        // A fixed placeholder would collide on the unique index the second time any RefTest is
        // anonymized, so each one gets its own value.
        var first = NewRefTest();
        var second = NewRefTest();

        first.Anonymize();
        second.Anonymize();

        Assert.NotEqual(first.Token, second.Token);
        Assert.StartsWith("erased-", first.Token, StringComparison.Ordinal);
        Assert.StartsWith("erased-", second.Token, StringComparison.Ordinal);
    }

    [Fact]
    public void Anonymize_KeepsTheConsentEvidence()
    {
        var refTest = NewRefTest();
        refTest.AcceptPrivacyNotice("2026-01-01");
        var acceptedAt = refTest.PrivacyNoticeAcceptedAt;

        refTest.Anonymize();

        // Neither field identifies the participant once name, email and token are gone, and the
        // controller must still be able to demonstrate that consent was given.
        Assert.Equal("2026-01-01", refTest.PrivacyNoticeVersion);
        Assert.Equal(acceptedAt, refTest.PrivacyNoticeAcceptedAt);
    }

    [Fact]
    public void Anonymize_IsIdempotent()
    {
        var refTest = NewRefTest();
        refTest.Anonymize();

        var tokenAfterFirst = refTest.Token;
        var anonymizedAtAfterFirst = refTest.AnonymizedAt;

        refTest.Anonymize();

        // A second call must not move the timestamp or mint a new token; both would misrepresent
        // when the erasure actually happened.
        Assert.Equal(tokenAfterFirst, refTest.Token);
        Assert.Equal(anonymizedAtAfterFirst, refTest.AnonymizedAt);
    }
}
