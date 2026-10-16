using System.Text.Json;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTests.Events;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public class RefTestDetailsUpdatedEventTests
{
    [Fact]
    public void UpdateBasicDetails_WhenOnlyEmailChanges_RecordsOnlyEmailInAuditEvent()
    {
        var refTest = RefTest.Create(
            titleId: Guid.NewGuid(),
            firstName: "Ada",
            lastName: "Lovelace",
            email: "ada@example.org",
            numberOfQuestions: 10,
            maxTimeInMinutes: 30,
            questionIds: ["q1"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: false);
        refTest.ClearDomainEvents();

        refTest.UpdateBasicDetails("Ada", "Lovelace", "ada.new@example.org");

        var detailsUpdated = Assert.IsType<RefTestDetailsUpdatedEvent>(Assert.Single(refTest.DomainEvents));
        using var changes = JsonDocument.Parse(JsonSerializer.Serialize(detailsUpdated.GetChanges()));
        Assert.Equal(
            new[] { "email" },
            changes.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(
            "ada@example.org",
            changes.RootElement.GetProperty("email").GetProperty("old").GetString());
        Assert.Equal(
            "ada.new@example.org",
            changes.RootElement.GetProperty("email").GetProperty("new").GetString());
    }
}
