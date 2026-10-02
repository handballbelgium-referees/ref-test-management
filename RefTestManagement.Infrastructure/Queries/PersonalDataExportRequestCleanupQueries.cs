using System.Linq.Expressions;
using Handball.Belgium.RefTestManagement.Domain.Privacy;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Queries;

/// <summary>Database-translatable selection for abandoned export-verification challenges.</summary>
public static class PersonalDataExportRequestCleanupQueries
{
    /// <summary>
    /// Matches unverified requests whose key has expired and which still retain challenge or
    /// recipient data that must be cleared.
    /// </summary>
    public static Expression<Func<PersonalDataExportRequest, bool>> IsDueForCleanup(DateTime now) =>
        request => request.VerifiedAt == null
                   && request.ExpiresAt <= now
                   && (request.Email != string.Empty
                       || request.KeyHash != null
                       || request.ProtectedDeliveryKey != null
                       || request.ChallengeEmailSentAt != null
                       || request.LastDeliveryAttemptAt != null
                       || request.DeliveryAttemptCount != 0);
}
