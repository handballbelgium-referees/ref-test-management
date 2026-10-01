using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Services;

internal static class RefTestTokenQueryExtensions
{
    public static IQueryable<RefTest> WithParticipantToken(this IQueryable<RefTest> query, string token)
    {
        var tokenHash = RefTest.HashToken(token);
        return query.Where(refTest => refTest.Token == tokenHash);
    }
}
