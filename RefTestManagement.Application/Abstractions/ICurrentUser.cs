namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

/// <summary>Identity and request correlation information for the current caller.</summary>
public interface ICurrentUser
{
    string DisplayName { get; }
    string Email { get; }
    string CorrelationId { get; }
    IReadOnlySet<string> Permissions { get; }
}
