using System.Reflection;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Privacy;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Api.Graphql.Types;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Infrastructure;
using HotChocolate.Authorization;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public class AuthorizationSchemaTests
{
    [Fact]
    public void PrivacyWithdrawalResolversArePublicMutationFields()
    {
        var mutationType = typeof(PrivacyWithdrawalMutations);
        Assert.NotNull(mutationType.GetCustomAttribute<MutationTypeAttribute>());

        var methods = mutationType
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["ConfirmPrivacyWithdrawalAsync", "RequestPrivacyWithdrawalAsync"],
            methods.Select(method => method.Name));
        Assert.DoesNotContain(
            methods,
            method => method.GetCustomAttribute<AuthorizeAttribute>() is not null);
    }

    [Fact]
    public async Task PublicWithdrawalSchemaUsesAnonymousMutationConventionFields()
    {
        await using var provider = BuildApiSchemaProvider();
        var executorProvider = provider.GetRequiredService<IRequestExecutorProvider>();
        var executor = await executorProvider.GetExecutorAsync(
            Assert.Single(executorProvider.SchemaNames),
            TestContext.Current.CancellationToken);
        var mutation = Assert.IsAssignableFrom<IComplexTypeDefinition>(executor.Schema.MutationType);
        var requestField = mutation.Fields.Single(field => field.Name == "requestPrivacyWithdrawal");
        var confirmationField = mutation.Fields.Single(field => field.Name == "confirmPrivacyWithdrawal");

        Assert.DoesNotContain(
            requestField.Directives,
            directive => directive.Definition.Name is "authorize" or "authorizeByRole");
        Assert.DoesNotContain(
            confirmationField.Directives,
            directive => directive.Definition.Name is "authorize" or "authorizeByRole");
        Assert.DoesNotContain(
            executor.Schema.QueryType.Fields,
            field => field.Name is "requestPrivacyWithdrawal" or "confirmPrivacyWithdrawal");
        // GraphQL GET requests execute queries only; keeping confirmation off Query prevents link prefetch.

        Assert.Equal(
            ["privacyWithdrawalRequestAcknowledgement"],
            AnonymousFields(Assert.IsAssignableFrom<IComplexTypeDefinition>(
                executor.Schema.Types.GetType<IComplexTypeDefinition>(
                    "RequestPrivacyWithdrawalPayload"))));
        Assert.Equal(
            ["privacyWithdrawalConfirmationResult"],
            AnonymousFields(Assert.IsAssignableFrom<IComplexTypeDefinition>(
                executor.Schema.Types.GetType<IComplexTypeDefinition>(
                    "ConfirmPrivacyWithdrawalPayload"))));
        Assert.Equal(
            ["acknowledged"],
            AnonymousFields(Assert.IsAssignableFrom<IComplexTypeDefinition>(
                executor.Schema.Types.GetType<IComplexTypeDefinition>(
                    "PrivacyWithdrawalRequestAcknowledgement"))));
        Assert.Equal(
            ["accepted"],
            AnonymousFields(Assert.IsAssignableFrom<IComplexTypeDefinition>(
                executor.Schema.Types.GetType<IComplexTypeDefinition>(
                    "PrivacyWithdrawalConfirmationResult"))));
    }

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
            refTest.Fields.Single(field => field.Name == "id").Type.ToString(),
            participantRefTest.Fields.Single(field => field.Name == "id").Type.ToString());
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
        Assert.Empty(participantQuestions.Arguments);
        Assert.DoesNotContain(participantQuestions.Arguments, argument => argument.Name == "includeIsCorrect");
        Assert.DoesNotContain(participantQuestions.Arguments, argument => argument.Name == "randomAnswerOrder");
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

    private static ServiceProvider BuildApiSchemaProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services
            .AddGraphQLServer()
            .AddQueryType()
            .AddMutationType()
            .AddSubscriptionType()
            .AddApiTypes()
            .AddQueryConventions()
            .AddMutationConventions()
            .AddInMemorySubscriptions()
            .RegisterDbContextFactory<RefTestManagementContext>()
            .AddProjections()
            .AddFiltering()
            .AddSorting()
            .AddCacheControl()
            .AddDefaultNodeIdSerializer(useUrlSafeBase64: true)
            .AddGlobalObjectIdentification(false)
            .AddAuthorization();

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
