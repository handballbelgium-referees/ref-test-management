using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Security;
using Microsoft.AspNetCore.Authorization;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public class AuthorizationTests
{
    [Fact]
    public async Task AnonymousUsersCannotSatisfyRefTestDetailPolicy()
    {
        var requirement = new TaskPermissionRequirement(Permissions.RefTests.ViewDetail);
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity()),
            null);

        await new TaskPermissionHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task RefTestDetailPermissionSatisfiesItsPolicy()
    {
        var requirement = new TaskPermissionRequirement(Permissions.RefTests.ViewDetail);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("permissions", Permissions.RefTests.ViewDetail)]));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await new TaskPermissionHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task QuestionDetailPermissionDoesNotGrantRefTestDetail()
    {
        var requirement = new TaskPermissionRequirement(Permissions.RefTests.ViewDetail);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("permissions", Permissions.RefTests.ViewDetailQuestions)]));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await new TaskPermissionHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task AnonymousUsersCannotSatisfyQuestionNumberAnyOfPolicy()
    {
        var requirement = new AnyTaskPermissionRequirement(
            Permissions.RefTests.ViewDetailQuestions,
            Permissions.Questions.Search,
            Permissions.Questions.View);
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity()),
            null);

        await new AnyTaskPermissionHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
