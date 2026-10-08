using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Queries;

/// <summary>
/// Database-translatable selection shared by privacy withdrawal and personal-data export.
/// </summary>
public static class PrivacyWithdrawalQueries
{
    /// <summary>
    /// Selects records eligible for a mailbox-address match. Status is intentionally not
    /// restricted: every non-anonymized RefTest belongs in a confirmed withdrawal snapshot.
    /// </summary>
    public static IQueryable<RefTest> EligibleRefTests(IQueryable<RefTest> refTests) =>
        refTests.Where(refTest => !refTest.IsAnonymized);

    /// <summary>Selects eligible records whose normalized-email lookup key matches.</summary>
    public static IQueryable<RefTest> MatchingRefTestsByEmailLookupKey(
        IQueryable<RefTest> refTests,
        byte[] emailLookupKey) =>
        EligibleRefTests(refTests)
            .Where(refTest => refTest.EmailLookupKey == emailLookupKey);

    /// <summary>Selects eligible legacy records that still need the application-side lookup-key backfill.</summary>
    public static IQueryable<RefTest> EligibleRefTestsMissingEmailLookupKey(
        IQueryable<RefTest> refTests) =>
        EligibleRefTests(refTests)
            .Where(refTest => refTest.EmailLookupKey == null);
}
