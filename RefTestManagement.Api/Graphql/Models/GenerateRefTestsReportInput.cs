using Handball.Belgium.RefTestManagement.Domain;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

public record GenerateRefTestsReportInput(
    [ID<RefTest>] List<Guid> Ids);
