namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Privacy;

/// <summary>Request data for a public mailbox-verification challenge.</summary>
public sealed record PersonalDataExportRequestInput(string Email);

/// <summary>
/// A deliberately generic acknowledgement. It is returned unchanged whether or not the address
/// matches a participant record.
/// </summary>
public sealed record PersonalDataExportRequestAcknowledgement(bool Acknowledged);

/// <summary>Result of attempting to consume a one-time export verification key.</summary>
public sealed record PersonalDataExportConfirmationResult(bool Confirmed);
