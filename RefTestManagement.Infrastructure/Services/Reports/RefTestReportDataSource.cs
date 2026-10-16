using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public sealed class RefTestReportDataSource(RefTestManagementContext context) : IRefTestReportDataSource
{
    public async Task<IReadOnlyList<RefTestReportSnapshot>> GetAsync(
        IReadOnlyCollection<Guid> refTestIds,
        CancellationToken cancellationToken)
    {
        var refTests = await context.RefTests
            .Include(refTest => refTest.Title)
            .Where(refTest => refTestIds.Contains(refTest.Id))
            .OrderBy(refTest => refTest.LastName)
            .ToListAsync(cancellationToken);

        return refTests.Select(refTest => new RefTestReportSnapshot(
            refTest.Id,
            refTest.Title?.Value ?? "Unknown",
            refTest.FirstName,
            refTest.LastName,
            refTest.StartedAt,
            refTest.CompletedAt,
            refTest.QuestionScore,
            refTest.QuestionTotal,
            refTest.AnswerScore,
            refTest.AnswerTotal,
            refTest.Percentage,
            refTest.Language,
            refTest.Duration)).ToArray();
    }
}
