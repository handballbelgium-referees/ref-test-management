namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Privacy;

/// <summary>Email address for a public consent-withdrawal challenge request.</summary>
public sealed record PrivacyWithdrawalRequestInput(string Email);

/// <summary>
/// A deliberately generic acknowledgement. It is returned unchanged whether or not the address
/// matches a participant record.
/// </summary>
public sealed record PrivacyWithdrawalRequestAcknowledgement(bool Acknowledged);

/// <summary>Minimal result indicating whether verified withdrawal work was durably accepted.</summary>
public sealed record PrivacyWithdrawalConfirmationResult(bool Accepted);
