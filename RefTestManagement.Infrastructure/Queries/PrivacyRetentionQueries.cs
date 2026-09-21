using System.Linq.Expressions;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Queries;

/// <summary>
/// The privacy retention rule, written so the database can evaluate it.
/// </summary>
/// <remarks>
/// This predicate decides which participants' personal data gets erased, so the cost of getting it
/// wrong runs in both directions and neither direction is visible. Match too little and personal
/// data is kept past its lawful basis, silently, for years. Match too much and a participant's
/// record is anonymized while it is still live — and erasure is irreversible, so there is nothing
/// to roll back to.
///
/// It lived inline in <c>PrivacyRetentionService</c>, which is a <c>BackgroundService</c> whose
/// only entry point is a timer loop. Nothing could assert on it without standing up a host and
/// waiting. Extracting it — the same shape as <see cref="RefTestExpirationQueries"/> — makes the
/// rule addressable on its own, which is what <c>PrivacyRetentionQueriesTests</c> pins.
/// </remarks>
public static class PrivacyRetentionQueries
{
    /// <summary>
    /// Matches ref tests whose personal data has outlived the retention window at
    /// <paramref name="cutoff"/>.
    /// </summary>
    /// <param name="cutoff">
    /// The oldest moment a record may be retained from. A record whose governing timestamp falls
    /// before this is due for erasure.
    /// </param>
    public static Expression<Func<RefTest, bool>> IsDueForErasure(DateTime cutoff) =>
        refTest =>
            // Already erased. Re-selecting it would be harmless but pointless work every day.
            !refTest.IsAnonymized
            && ((
                    // The two statuses that stamp their own terminal timestamp; retention runs
                    // from the moment the test actually ended.
                    refTest.Status == RefTestStatus.Completed && refTest.CompletedAt < cutoff)
                || (refTest.Status == RefTestStatus.Expired && refTest.ExpiredAt < cutoff)
                || (
                    // Neither status is reached by the expiration sweep, so without this clause
                    // they would retain personal data forever. PendingApproval is genuinely
                    // terminal; Rejected is not — Approve() accepts a rejected test and returns it
                    // to Pending — but a test still sitting rejected after the entire retention
                    // window is abandoned, and keeping personal data for it has no lawful basis.
                    // Erasure makes that permanent: Approve() throws once IsAnonymized, so a
                    // rejected test cannot be revived afterwards. Neither status stamps a
                    // completion timestamp, so retention runs from CreatedAt.
                    (refTest.Status == RefTestStatus.PendingApproval ||
                     refTest.Status == RefTestStatus.Rejected)
                    && refTest.CreatedAt < cutoff));

    // Deliberately absent:
    //   Pending and InProgress are live. A test a participant could still be sitting must never be
    //   erased on a timer, however old the record is.
}
