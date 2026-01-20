using System.Linq.Expressions;
using Handball.Belgium.RefTestManagement.Domain;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

public static class RefTestTitleMappings
{
    public static readonly Expression<Func<RefTestTitle, RefTestTitleDto>> ToDto =
        refTestTitle => new RefTestTitleDto
        {
            Id = refTestTitle.Id,
            Value = refTestTitle.Value
        };
}