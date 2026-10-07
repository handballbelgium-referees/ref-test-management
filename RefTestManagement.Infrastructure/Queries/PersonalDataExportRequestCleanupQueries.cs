using System.Linq.Expressions;
using Handball.Belgium.RefTestManagement.Domain.Privacy;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Queries;

/// <summary>Database-translatable selection for expired challenges and terminal, inactive export requests.</summary>
public static class PersonalDataExportRequestCleanupQueries
{
    /// <summary>
    /// Matches unverified requests whose key has expired and which still retain challenge or
    /// recipient data, and verified requests tied to a terminal delivery job with no active
    /// duplicate delivery job.
    /// </summary>
    public static Expression<Func<PersonalDataExportRequest, bool>> IsDueForCleanup(
        DateTime now,
        Guid[] terminalDeliveryRequestIds) =>
        request =>
            (request.VerifiedAt == null
             && request.ExpiresAt <= now
             && (request.Email != string.Empty
                 || request.KeyHash != null
                 || request.ProtectedDeliveryKey != null
                 || request.ChallengeEmailSentAt != null
                 || request.LastDeliveryAttemptAt != null
                 || request.DeliveryAttemptCount != 0))
            || (request.VerifiedAt != null
                && request.Email != string.Empty
                && terminalDeliveryRequestIds.Contains(request.Id));

    /// <summary>Matches expired unverified requests only.</summary>
    public static Expression<Func<PersonalDataExportRequest, bool>> IsDueForCleanup(DateTime now) =>
        IsDueForCleanup(now, []);
}
