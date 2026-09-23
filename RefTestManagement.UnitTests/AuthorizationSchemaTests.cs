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
        var participantRefTest = Assert.IsType<IComplexTypeDefinition>(
            executor.Schema.Types.GetType<IComplexTypeDefinition>("ParticipantRefTest"), exactMatch: false);
        var participantQuestion = Assert.IsType<IComplexTypeDefinition>(
            executor.Schema.Types.GetType<IComplexTypeDefinition>("ParticipantQuestion"), exactMatch: false);
        var participantAnswer = Assert.IsType<IComplexTypeDefinition>(
            executor.Schema.Types.GetType<IComplexTypeDefinition>("ParticipantAnswer"), exactMatch: false);

        Assert.Equal(["id"], AnonymousFields(refTest));

        Assert.Equal(["id"], AnonymousFields(question));
        Assert.Equal(["id"], AnonymousFields(answer));

        Assert.DoesNotContain("number", AnonymousFields(question));
        Assert.DoesNotContain("number", AnonymousFields(answer));
        Assert.DoesNotContain("isCorrect", AnonymousFields(answer));
        Assert.Equal(
            [
                "answerScore",
                "answerTotal",
                "currentQuestionIndex",
                "email",
                "id",
                "maxTimeInMinutes",
                "name",
                "numberOfQuestions",
                "percentage",
                "questionScore",
                "questionTotal",
                "questions",
                "resultsSent",
                "selectedAnswerIds",
                "sendResultsAutomatically",
                "startedAt",
                "status"
            ],
            AnonymousFields(participantRefTest));
        Assert.Equal(["answers", "id", "number", "phrase"], AnonymousFields(participantQuestion));
        Assert.Equal(["id", "isCorrect", "number", "phrase"], AnonymousFields(participantAnswer));
        var participantQuestions = participantRefTest.Fields.Single(field => field.Name == "questions");
        Assert.DoesNotContain(participantQuestions.Arguments, argument => argument.Name == "includeIsCorrect");
        Assert.DoesNotContain(executor.Schema.QueryType.Fields, field => field.Name is "node" or "nodes");
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
            .AddType<ParticipantRefTestType>()
            .AddType<ParticipantQuestionType>()
            .AddType<ParticipantAnswerType>()
            .AddDefaultNodeIdSerializer(useUrlSafeBase64: true)
            .AddGlobalObjectIdentification(false)
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
