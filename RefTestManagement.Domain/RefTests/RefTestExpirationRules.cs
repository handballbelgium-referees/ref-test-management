namespace Handball.Belgium.RefTestManagement.Domain.RefTests;

public static class RefTestExpirationRules
{
    public static readonly TimeSpan DeadlineGrace = TimeSpan.FromSeconds(60);

    public static bool IsInProgressDue(
        DateTime startedAt,
        int maxTimeInMinutes,
        DateTime now)
        => startedAt.AddMinutes(maxTimeInMinutes).Add(DeadlineGrace) <= now;

    public static bool IsPendingDue(
        DateTime createdAt,
        TimeSpan expirationIfNotStarted,
        DateTime now)
        => createdAt.Add(expirationIfNotStarted) <= now;
}
