using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Approval;

public record ApproveRefTestsInput([property: ID<RefTest>] List<Guid> Ids);
public record RejectRefTestsInput([property: ID<RefTest>] List<Guid> Ids, string Reason);
