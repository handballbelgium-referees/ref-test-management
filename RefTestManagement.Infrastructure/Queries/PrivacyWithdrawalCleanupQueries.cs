using System.Linq.Expressions;
using Handball.Belgium.RefTestManagement.Domain.Privacy;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Queries;

/// <summary>Database-translatable selection of expired withdrawal challenges.</summary>
public static class PrivacyWithdrawalCleanupQueries
{
    /// <summary>
    /// Matches unverified challenges whose key has expired and which still retain recipient or
    /// key state that must be cleared.
    /// </summary>
    public static Expression<Func<PrivacyWithdrawalChallenge, bool>> IsDueForChallengeCleanup(DateTime now) =>
        challenge => challenge.VerifiedAt == null
                     && challenge.ExpiresAt <= now
                     && (challenge.Email != string.Empty
                         || challenge.KeyHash != null
                         || challenge.ProtectedDeliveryKey != null
                         || challenge.ChallengeEmailSentAt != null
                         || challenge.LastDeliveryAttemptAt != null
                         || challenge.DeliveryAttemptCount != 0);
}
