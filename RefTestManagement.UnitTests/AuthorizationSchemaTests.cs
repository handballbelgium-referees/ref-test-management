using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Privacy;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Api.Graphql.Types;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.AspNetCore;
using HotChocolate.Authorization;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public class AuthorizationSchemaTests
{
    private const string NestedQuestionId = "nested-question-id";
    private const string ResultQuestionId = "result-question-id";
    private const string ResultAnswerId = "result-answer-id";
    private const string SelectedAnswerId = "selected-answer-id";

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

        Assert.Empty(AnonymousFields(question));
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

    [Fact]
    public async Task RefTestQuestionIdentifiersRequireQuestionPermissionButResultIdsRemainAvailable()
    {
        var questionsService =
            DispatchProxy.Create<IIhfRulesQuestionsService, QuestionAuthorizationQuestionsService>();
        await using var app = await BuildQuestionAuthorizationTestServer(questionsService);
        var client = app.GetTestClient();

        using var deniedQuestionsExecution = await ExecuteGraphQlAsync(
            client,
            "query { refTest { questions { id } } }",
            TestContext.Current.CancellationToken,
            Permissions.RefTests.ViewDetail);
        Assert.NotEmpty(GraphQlErrors(deniedQuestionsExecution));
        Assert.DoesNotContain(NestedQuestionId, deniedQuestionsExecution.RootElement.GetRawText());

        using var deniedQuestionIdExecution = await ExecuteGraphQlAsync(
            client,
            "query { question { id } }",
            TestContext.Current.CancellationToken,
            Permissions.RefTests.ViewDetail);
        Assert.NotEmpty(GraphQlErrors(deniedQuestionIdExecution));
        Assert.DoesNotContain(NestedQuestionId, deniedQuestionIdExecution.RootElement.GetRawText());

        using var resultIdsExecution = await ExecuteGraphQlAsync(
            client,
            "query { refTest { wrongQuestionIds wrongAnswerIds selectedAnswerIds } }",
            TestContext.Current.CancellationToken,
            Permissions.RefTests.ViewDetail);
        Assert.Empty(GraphQlErrors(resultIdsExecution));
        var result = resultIdsExecution.RootElement.GetProperty("data").GetProperty("refTest");
        Assert.Equal(ResultQuestionId, result.GetProperty("wrongQuestionIds")[0].GetString());
        Assert.Equal(ResultAnswerId, result.GetProperty("wrongAnswerIds")[0].GetString());
        Assert.Equal(SelectedAnswerId, result.GetProperty("selectedAnswerIds")[0].GetString());

        using var allowedQuestionsExecution = await ExecuteGraphQlAsync(
            client,
            "query { refTest { questions { id } } }",
            TestContext.Current.CancellationToken,
            Permissions.RefTests.ViewDetail,
            Permissions.RefTests.ViewDetailQuestions);
        Assert.Empty(GraphQlErrors(allowedQuestionsExecution));
        Assert.Equal(
            NestedQuestionId,
            allowedQuestionsExecution.RootElement.GetProperty("data")
                .GetProperty("refTest").GetProperty("questions")[0].GetProperty("id").GetString());

        using var questionQueryExecution = await ExecuteGraphQlAsync(
            client,
            "query { question { id } }",
            TestContext.Current.CancellationToken,
            Permissions.Questions.View);
        Assert.Empty(GraphQlErrors(questionQueryExecution));
        Assert.Equal(
            NestedQuestionId,
            questionQueryExecution.RootElement.GetProperty("data")
                .GetProperty("question").GetProperty("id").GetString());
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

    private static async Task<WebApplication> BuildQuestionAuthorizationTestServer(
        IIhfRulesQuestionsService questionsService)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTaskBasedAuthorization();
        builder.Services.AddSingleton(questionsService);
        builder.Services
            .AddGraphQLServer()
            .AddQueryType<QuestionAuthorizationSchemaQuery>()
            .AddType<RefTestType>()
            .AddType<QuestionType>()
            .AddType<AnswerType>()
            .AddDefaultNodeIdSerializer(useUrlSafeBase64: true)
            .AddGlobalObjectIdentification(false)
            .AddAuthorization();

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                context.Request.Headers["X-Permissions"]
                    .Select(permission => new Claim("permissions", permission!)),
                "test"));
            await next();
        });
        app.MapGraphQL();
        await app.StartAsync();
        return app;
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

    private static async Task<JsonDocument> ExecuteGraphQlAsync(
        HttpClient client,
        string query,
        CancellationToken cancellationToken,
        params string[] permissions)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-Permissions", permissions);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
    }

    private static JsonElement[] GraphQlErrors(JsonDocument result) =>
        result.RootElement.TryGetProperty("errors", out var errors)
            ? [.. errors.EnumerateArray()]
            : [];

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

    public sealed class QuestionAuthorizationSchemaQuery
    {
        public RefTestDto RefTest => new()
        {
            Id = Guid.Empty,
            FirstName = "Test",
            LastName = "User",
            FullName = "Test User",
            Email = "test@example.org",
            QuestionIds = ["source-question-id"],
            WrongQuestionIds = [ResultQuestionId],
            WrongAnswerIds = [ResultAnswerId],
            SelectedAnswerIds = [SelectedAnswerId]
        };

        public Question Question => new(
            NestedQuestionId,
            new Dictionary<string, string>(),
            []);
    }

    public class QuestionAuthorizationQuestionsService : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IIhfRulesQuestionsService.GetQuestionsByIdAsync)
                ? Task.FromResult<List<Question>>(
                    [new Question(NestedQuestionId, new Dictionary<string, string>(), [])])
                : throw new NotSupportedException($"Unexpected {targetMethod?.Name} call.");
    }
}
