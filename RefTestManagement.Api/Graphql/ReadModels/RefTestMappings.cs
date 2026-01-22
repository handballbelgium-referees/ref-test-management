using System.Linq.Expressions;
using Handball.Belgium.RefTestManagement.Domain;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

public static class RefTestMappings
{
    public static readonly Expression<Func<RefTest, RefTestDto>> ToDto =
        refTest => refTest.ToDto();
}