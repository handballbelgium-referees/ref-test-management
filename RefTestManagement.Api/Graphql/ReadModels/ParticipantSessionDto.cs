namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

/// <summary>A short-lived credential used to resume a participant's RefTest session.</summary>
/// <param name="SessionToken">The protected participant session credential.</param>
public sealed record ParticipantSessionDto(string SessionToken);
