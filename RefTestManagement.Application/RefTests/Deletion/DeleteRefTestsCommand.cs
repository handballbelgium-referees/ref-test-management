namespace Handball.Belgium.RefTestManagement.Application.RefTests.Deletion;

public sealed record DeleteRefTestsCommand(IReadOnlyList<Guid> Ids);
