namespace Handball.Belgium.RefTestManagement.Application.Models;

/// <summary>
/// Minimal outbox payload for sending a privacy-withdrawal verification email. It contains no
/// recipient address or verification key.
/// </summary>
public sealed record PrivacyWithdrawalChallengeEmailPayload(Guid ChallengeId) : IJobPayload;

/// <summary>
/// Minimal outbox payload for processing a bulk withdrawal. RefTest IDs remain in durable target
/// rows and are never copied into the job.
/// </summary>
public sealed record PrivacyWithdrawalBatchPayload(Guid BatchId) : IJobPayload;
