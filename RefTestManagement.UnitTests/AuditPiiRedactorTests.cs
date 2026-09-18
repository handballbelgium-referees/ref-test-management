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

    [Theory]
    [InlineData("""{"FirstName":"John"}""", "FirstName")]
    [InlineData("""{"LastName":"Doe"}""", "LastName")]
    [InlineData("""{"EMAIL":"john@example.com"}""", "EMAIL")]
    public void RedactData_MatchesPiiKeysRegardlessOfCasing(string data, string key)
    {
        // Domain events serialize camelCase, but the audit interceptor's property-diff path
        // writes raw PascalCase EF property names. Both have to be redacted.
        var node = JsonNode.Parse(AuditPiiRedactor.RedactData(data)!)!.AsObject();

        Assert.Equal(AuditPiiRedactor.RedactedValue, (string?)node[key]);
    }

    [Fact]
    public void RedactData_MatchesDiffKeysRegardlessOfCasing()
    {
        var redacted = AuditPiiRedactor.RedactData("""{"Email":{"Old":"a@x.be","New":"b@x.be"}}""");

        Assert.DoesNotContain("a@x.be", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("b@x.be", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactData_PreservesTheOriginalKeyCasing()
    {
        // Redaction must not rename properties: the audit UI and any downstream reader still
        // key off the original names.
        var node = JsonNode.Parse(AuditPiiRedactor.RedactData("""{"FirstName":"John"}""")!)!.AsObject();

        Assert.True(node.ContainsKey("FirstName"));
        Assert.Single(node);
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
