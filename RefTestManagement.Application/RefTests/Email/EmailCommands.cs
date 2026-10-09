namespace Handball.Belgium.RefTestManagement.Application.RefTests.Email;

public sealed record SendInvitationsCommand(IReadOnlyList<Guid> Ids);

public sealed record SendResultsCommand(IReadOnlyList<Guid> Ids);

public sealed record SendReportCommand(IReadOnlyList<Guid> Ids);
