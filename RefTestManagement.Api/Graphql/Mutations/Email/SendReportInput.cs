using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Email;
public record SendReportInput(
    [property: ID<RefTest>] List<Guid> Ids);
