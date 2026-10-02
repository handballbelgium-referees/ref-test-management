using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Queries;

/// <summary>Database-translatable selection shared by withdrawal request and confirmation.</summary>
public static class PrivacyWithdrawalQueries
{
    /// <summary>
    /// Selects records eligible for a mailbox-address match. Status is intentionally not
    /// restricted: every non-anonymized RefTest belongs in a confirmed withdrawal snapshot.
    /// </summary>
    public static IQueryable<RefTest> EligibleRefTests(IQueryable<RefTest> refTests) =>
        refTests.Where(refTest => !refTest.IsAnonymized);
}
