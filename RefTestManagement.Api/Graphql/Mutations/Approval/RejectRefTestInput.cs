using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Approval;

public record RejectRefTestInput([property: ID<RefTest>] Guid RefTestId, string Reason);
