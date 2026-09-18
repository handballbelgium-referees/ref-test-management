using Handball.Belgium.RefTestManagement.Infrastructure.Logging;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Covers the masking applied to text the application did not compose itself — exception
/// messages and third-party API responses — before it is logged or persisted to
/// <c>Job.ErrorMessage</c>. That column is outside the privacy erasure path, so an address that
/// slips through survives an erasure request.
/// </summary>
public class LogRedactionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskEmail_ReportsAbsenceRatherThanEchoingEmptyInput(string? email)
    {
        Assert.Equal("(none)", LogRedaction.MaskEmail(email));
    }

    [Fact]
    public void MaskEmail_KeepsTheDomainAndTheFirstCharacterOnly()
    {
        Assert.Equal("j***@example.com", LogRedaction.MaskEmail("john.doe@example.com"));
    }

    [Fact]
    public void MaskEmail_HidesTheTagOfAPlusAddressedAddress()
    {
        // Plus-addressing puts extra identifying detail in the local part, which is exactly the
        // part being dropped.
        Assert.Equal("j***@example.com", LogRedaction.MaskEmail("john+reftest@example.com"));
    }

    [Theory]
    [InlineData("@example.com")]
    [InlineData("not-an-email")]
    public void MaskEmail_FallsBackToAFullMask_WhenThereIsNoUsableLocalPart(string input)
    {
        Assert.Equal("***", LogRedaction.MaskEmail(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MaskEmailsInText_PassesThroughEmptyInput(string? text)
    {
        Assert.Equal(text, LogRedaction.MaskEmailsInText(text));
    }

    [Fact]
    public void MaskEmailsInText_MasksEveryAddressInASentence()
    {
        var masked = LogRedaction.MaskEmailsInText(
            "Delivery to john.doe@example.com failed; retry queued for jane@other.org");

        Assert.Equal(
            "Delivery to j***@example.com failed; retry queued for j***@other.org",
            masked);
    }

    [Fact]
    public void MaskEmailsInText_MasksAddressesEmbeddedInAJsonApiResponse()
    {
        var masked = LogRedaction.MaskEmailsInText(
            """{"code":"invalid_parameter","message":"to[0].email 'john.doe@example.com' is invalid"}""");

        Assert.DoesNotContain("john.doe", masked, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("j***@example.com", masked, StringComparison.Ordinal);

        // The surrounding diagnostic detail is what makes the message worth keeping.
        Assert.Contains("invalid_parameter", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void MaskEmailsInText_LeavesTextWithoutAddressesUntouched()
    {
        const string text = "SMTP connection timed out after 30s";

        Assert.Equal(text, LogRedaction.MaskEmailsInText(text));
    }
}
