using System.Linq.Expressions;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Queries;

/// <summary>
/// The expiration rules, written so the database can evaluate them.
/// </summary>
/// <remarks>
/// The sweep used to load every non-terminal ref test into memory every five minutes and filter it
/// in process. That is the whole active table, read repeatedly to discard almost all of it.
/// Expressed as a predicate the provider can translate, the same sweep touches only the rows that
/// are actually due.
///
/// This is the delicate kind of change: each branch encodes a real rule, and a predicate that
/// quietly drops one would stop expiring a whole category of test without anything failing.
/// The branches below map one-to-one onto the states the in-process check handled, and
/// <c>RefTestExpirationQueriesTests</c> pins both the behaviour and the fact that all four
/// providers translate it instead of falling back to the client.
/// </remarks>
public static class RefTestExpirationQueries
{
    /// <summary>
    /// Matches ref tests that are due for expiration at <paramref name="now"/>.
    /// </summary>
    /// <param name="now">The moment to evaluate against.</param>
    /// <param name="expirationIfNotStarted">
    /// How long a test may sit unstarted before it expires.
    /// </param>
    public static Expression<Func<RefTest, bool>> IsDueForExpiration(
        DateTime now,
        TimeSpan expirationIfNotStarted)
    {
        // Subtracting the window from `now` keeps this side of the comparison a constant, which
        // every provider can parameterise. Adding the window to CreatedAt instead would put date
        // arithmetic on a column for no benefit.
        var unstartedCutoff = now - expirationIfNotStarted;

        return refTest =>
            // An anonymized test has no participant left to expire, and erasure already moved it
            // out of the active set.
            !refTest.IsAnonymized
            && ((
                    // Ran out of time. MaxTimeInMinutes is per test and can be extended while the
                    // test is in progress, so this arithmetic has to read the column.
                    refTest.Status == RefTestStatus.InProgress
                    && refTest.StartedAt != null
                    && refTest.StartedAt.Value.AddMinutes(refTest.MaxTimeInMinutes) <= now)
                || (
                    // Never started, and out of time to start.
                    refTest.Status == RefTestStatus.Pending
                    && refTest.CreatedAt <= unstartedCutoff));

        // Deliberately absent, each for its own reason:
        //   Completed and Expired are terminal.
        //   PendingApproval and Rejected have never been handed to a participant, so no clock is
        //   running; privacy retention is what eventually clears them.
    }
}
