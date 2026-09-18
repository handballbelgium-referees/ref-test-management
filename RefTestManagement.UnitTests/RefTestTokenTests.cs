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
    public void Create_GeneratesA128BitLowercaseHexToken()
    {
        var refTest = NewRefTest();

        // 32 hex characters is 128 bits, and matches the width of the value this used to produce,
        // so stored tokens and invitation URLs keep the same shape.
        Assert.Equal(32, refTest.Token.Length);
        Assert.True(IsLowercaseHex(refTest.Token), $"Token was not lowercase hex: {refTest.Token}");
    }

    [Fact]
    public void Create_GeneratesADistinctTokenEachTime()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => NewRefTest().Token).ToList();

        Assert.Equal(tokens.Count, tokens.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void RegenerateToken_ReplacesTheTokenWithAFreshOne()
    {
        var refTest = NewRefTest();
        var original = refTest.Token;

        refTest.RegenerateToken();

        Assert.NotEqual(original, refTest.Token);
        Assert.Equal(32, refTest.Token.Length);
        Assert.True(IsLowercaseHex(refTest.Token), $"Token was not lowercase hex: {refTest.Token}");
    }

    [Fact]
    public void HardReset_IssuesANewTokenSoTheOldInvitationStopsWorking()
    {
        var refTest = NewRefTest();
        refTest.AcceptPrivacyNotice("v1");
        refTest.Start("v1");
        var tokenWhileInProgress = refTest.Token;

        refTest.HardReset();

        Assert.NotEqual(tokenWhileInProgress, refTest.Token);
        Assert.True(IsLowercaseHex(refTest.Token), $"Token was not lowercase hex: {refTest.Token}");
    }
}
