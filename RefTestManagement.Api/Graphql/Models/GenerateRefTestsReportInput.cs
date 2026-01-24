using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

public record GenerateRefTestsReportInput(
    [property: ID<RefTestDto>] List<Guid> Ids);
