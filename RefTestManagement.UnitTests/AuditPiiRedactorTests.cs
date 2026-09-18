using System.Text.Json;
using System.Text.Json.Nodes;
using Handball.Belgium.RefTestManagement.AuditLog;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Covers the redactor that both the privacy erasure path and the audit retention sweep depend
/// on. Nothing else removes name and email from audit payloads, so a silent failure here means
/// personal data outlives an erasure request.
/// </summary>
public class AuditPiiRedactorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RedactData_ReturnsInputUnchanged_WhenThereIsNothingToRedact(string? data)
    {
        Assert.Equal(data, AuditPiiRedactor.RedactData(data));
    }

    [Fact]
    public void RedactData_LeavesNonObjectPayloadsAlone()
    {
        const string data = "[1,2,3]";

        Assert.Equal(data, AuditPiiRedactor.RedactData(data));
    }

    [Fact]
    public void RedactData_RedactsTheFlatPayloadShape()
    {
        var redacted = AuditPiiRedactor.RedactData(
            """{"firstName":"John","lastName":"Doe","email":"john.doe@example.com","score":12}""");

        var node = JsonNode.Parse(redacted!)!.AsObject();

        Assert.Equal(AuditPiiRedactor.RedactedValue, (string?)node["firstName"]);
        Assert.Equal(AuditPiiRedactor.RedactedValue, (string?)node["lastName"]);
        Assert.Equal(AuditPiiRedactor.RedactedValue, (string?)node["email"]);

        // Everything that is not personal data has to survive, or the audit trail loses the
        // accountability value that justifies keeping the row at all.
        Assert.Equal(12, (int?)node["score"]);
    }

    [Fact]
    public void RedactData_RedactsBothSidesOfTheDiffPayloadShape()
    {
        var redacted = AuditPiiRedactor.RedactData(
            """{"firstName":{"old":"John","new":"Jane"},"email":{"oldValue":"a@x.be","newValue":"b@x.be"}}""");

        var node = JsonNode.Parse(redacted!)!.AsObject();

        Assert.Equal(AuditPiiRedactor.RedactedValue, (string?)node["firstName"]!["old"]);
        Assert.Equal(AuditPiiRedactor.RedactedValue, (string?)node["firstName"]!["new"]);
        Assert.Equal(AuditPiiRedactor.RedactedValue, (string?)node["email"]!["oldValue"]);
        Assert.Equal(AuditPiiRedactor.RedactedValue, (string?)node["email"]!["newValue"]);
    }

    [Fact]
    public void RedactData_IsIdempotent()
    {
        // The retention sweep re-reads rows an earlier deployment may already have redacted, so
        // a second pass must not corrupt them.
        var once = AuditPiiRedactor.RedactData("""{"firstName":"John","email":"john@example.com"}""");
        var twice = AuditPiiRedactor.RedactData(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void RedactData_LeavesNullValuedPiiKeysAlone()
    {
        const string data = """{"firstName":null,"score":3}""";

        Assert.Equal(data, AuditPiiRedactor.RedactData(data));
    }

    [Fact]
    public void RedactData_DoesNotMatchKeysOnDifferentCasing()
    {
        // Documents current behaviour rather than endorsing it: payloads are serialized with
        // camelCase naming, so only that casing is produced. If a payload ever arrives in
        // PascalCase this test is the thing that fails and says so.
        const string data = """{"FirstName":"John"}""";

        Assert.Equal(data, AuditPiiRedactor.RedactData(data));
    }

    [Fact]
    public void RedactData_LeavesNoAddressBehind_ForARealisticCreatedPayload()
    {
        var payload = JsonSerializer.Serialize(new
        {
            titleId = Guid.NewGuid(),
            firstName = "John",
            lastName = "Doe",
            email = "john.doe@example.com",
            numberOfQuestions = 20
        });

        var redacted = AuditPiiRedactor.RedactData(payload);

        Assert.DoesNotContain("john.doe@example.com", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("John", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("Doe", redacted, StringComparison.Ordinal);
    }
}
