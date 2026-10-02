using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Services;

internal static class RefTestTokenQueryExtensions
{
    public static IQueryable<RefTest> WithParticipantToken(this IQueryable<RefTest> query, string token)
    {
        var tokenHash = RefTest.HashToken(token);
        return query.Where(refTest => refTest.Token == tokenHash);
    }

    public static async Task<RefTest?> FindByParticipantCredentialAsync(
        this IQueryable<RefTest> query,
        string credential,
        IRefTestSessionTokenService sessionTokenService,
        CancellationToken cancellationToken)
    {
        if (RefTest.IsValidTokenFormat(credential))
        {
            return await query
                .WithParticipantToken(credential)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!sessionTokenService.TryUnprotect(credential, out var claims) || claims is null)
            return null;

        var refTest = await query.FirstOrDefaultAsync(
            candidate =>
                candidate.Id == claims.RefTestId
                && candidate.Token == claims.InvitationTokenHash,
            cancellationToken);
        return refTest is not null && sessionTokenService.IsValidFor(claims, refTest)
            ? refTest
            : null;
    }
}
