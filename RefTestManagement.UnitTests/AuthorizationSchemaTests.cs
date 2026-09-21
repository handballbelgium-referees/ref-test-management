using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Api.Graphql.Types;
using Handball.Belgium.RefTestManagement.Application.Models;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public class AuthorizationSchemaTests
{
    [Fact]
    public async Task AnonymousFieldExposureMatchesTheParticipantContract()
    {
        await using var provider = BuildSchemaProvider();
        var executorProvider = provider.GetRequiredService<IRequestExecutorProvider>();
        var executor = await executorProvider.GetExecutorAsync(
            Assert.Single(executorProvider.SchemaNames),
            TestContext.Current.CancellationToken);

        var refTest = Assert.IsType<IComplexTypeDefinition>(
            executor.Schema.Types.GetType<IComplexTypeDefinition>("RefTest"), exactMatch: false);
        var question = Assert.IsType<IComplexTypeDefinition>(
            executor.Schema.Types.GetType<IComplexTypeDefinition>("Question"), exactMatch: false);
        var answer = Assert.IsType<IComplexTypeDefinition>(
            executor.Schema.Types.GetType<IComplexTypeDefinition>("Answer"), exactMatch: false);

        Assert.Equal(
            [
                "answerScore",
                "answerTotal",
                "currentQuestionIndex",
                "email",
                "maxTimeInMinutes",
                "name",
                "numberOfQuestions",
                "percentage",
                "questionScore",
                "questionTotal",
                "questions",
                "selectedAnswerIds",
                "sendResultsAutomatically",
                "startedAt",
                "title",
                "wrongAnswerIds",
                "wrongQuestionIds"
            ],
            AnonymousFields(refTest));

        Assert.Equal(["answers", "id", "phrase"], AnonymousFields(question));
        Assert.Equal(["id", "phrase"], AnonymousFields(answer));

        Assert.DoesNotContain("number", AnonymousFields(question));
        Assert.DoesNotContain("number", AnonymousFields(answer));
        Assert.DoesNotContain("isCorrect", AnonymousFields(answer));
        Assert.DoesNotContain("firstName", AnonymousFields(refTest));
        Assert.DoesNotContain("lastName", AnonymousFields(refTest));
        Assert.DoesNotContain("token", AnonymousFields(refTest));
    }

    private static ServiceProvider BuildSchemaProvider()
    {
        var services = new ServiceCollection();
        services
            .AddGraphQLServer()
            .AddQueryType<SchemaQuery>()
            .AddType<RefTestType>()
            .AddType<QuestionType>()
            .AddType<AnswerType>()
            .AddAuthorization();

        services.AddAuthorization();
        return services.BuildServiceProvider();
    }

    private static string[] AnonymousFields(IComplexTypeDefinition type) =>
        [.. type.Fields
            .Where(field => !field.IsIntrospectionField)
            .Where(field => !field.Directives.Any(directive =>
                directive.Definition.Name is "authorize" or "authorizeByRole"))
            .Select(field => field.Name)
            .OrderBy(name => name, StringComparer.Ordinal)];

    public sealed class SchemaQuery
    {
        public RefTestDto? RefTest => null;
        public Question? Question => null;
        public Answer? Answer => null;
    }
}
