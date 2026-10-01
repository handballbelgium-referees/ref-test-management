using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// The invitation token is the only credential guarding a participant's personal data and their
/// results, so it has to be unguessable. GUIDs are unique but carry no secrecy guarantee, which is
/// why these assert on a cryptographic RNG's output shape rather than just on uniqueness.
/// </summary>
public class RefTestTokenTests
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

    private static bool IsLowercaseHex(string value) =>
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    [Fact]
    public void Create_GeneratesA128BitTokenAndStoresOnlyItsSha256Hash()
    {
        var refTest = NewRefTest();
        var token = refTest.GetIssuedToken();

        Assert.Equal(32, token.Length);
        Assert.True(IsLowercaseHex(token), $"Issued token was not lowercase hex: {token}");
        Assert.Equal(64, refTest.Token.Length);
        Assert.Equal(RefTest.HashToken(token), refTest.Token);
        Assert.NotEqual(token, refTest.Token);
    }

    [Fact]
    public void Create_GeneratesADistinctTokenEachTime()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => NewRefTest().GetIssuedToken()).ToList();

        Assert.Equal(tokens.Count, tokens.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void RegenerateToken_ReplacesTheTokenWithAFreshOne()
    {
        var refTest = NewRefTest();
        var original = refTest.GetIssuedToken();
        refTest.StoreProtectedInvitationToken("protected-token");

        refTest.RegenerateToken();

        var issuedToken = refTest.GetIssuedToken();
        Assert.NotEqual(original, issuedToken);
        Assert.Equal(32, issuedToken.Length);
        Assert.True(IsLowercaseHex(issuedToken), $"Issued token was not lowercase hex: {issuedToken}");
        Assert.Equal(RefTest.HashToken(issuedToken), refTest.Token);
        Assert.Null(refTest.ProtectedInvitationToken);
    }

    [Fact]
    public void SendInvitation_ClearsTheProtectedRetryToken()
    {
        var refTest = NewRefTest();
        refTest.StoreProtectedInvitationToken("protected-token");

        refTest.SendInvitation();

        Assert.NotNull(refTest.InvitationSentAt);
        Assert.Null(refTest.ProtectedInvitationToken);
    }

    [Fact]
    public void Anonymize_ClearsTheProtectedRetryToken()
    {
        var refTest = NewRefTest();
        refTest.StoreProtectedInvitationToken("protected-token");

        refTest.Anonymize();

        Assert.Null(refTest.ProtectedInvitationToken);
    }

    [Fact]
    public void HardReset_IssuesANewTokenSoTheOldInvitationStopsWorking()
    {
        var refTest = NewRefTest();
        refTest.AcceptPrivacyNotice("v1");
        refTest.Start("v1");
        var tokenWhileInProgress = refTest.GetIssuedToken();

        refTest.HardReset();

        var issuedToken = refTest.GetIssuedToken();
        Assert.NotEqual(tokenWhileInProgress, issuedToken);
        Assert.True(IsLowercaseHex(issuedToken), $"Issued token was not lowercase hex: {issuedToken}");
        Assert.Equal(RefTest.HashToken(issuedToken), refTest.Token);
    }
}
