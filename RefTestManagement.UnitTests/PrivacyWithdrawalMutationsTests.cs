using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Privacy;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class PrivacyWithdrawalMutationsTests
{
    private const string ClientAddress = "192.0.2.10";
    private const string ParticipantEmail = "participant@example.org";
    private const string ValidKey = "valid-one-time-key";

    [Fact]
    public async Task RequestAcknowledgementIsUniformAndContainsNoParticipantData()
    {
        var service = new RecordingPrivacyWithdrawalRequestService();
        using var rateLimiter = NewRateLimiter(requestLimit: 2);
        var contextAccessor = NewHttpContextAccessor();
        var clientIpResolver = new FixedClientIpResolver();

        var matchingResult = await PrivacyWithdrawalMutations.RequestPrivacyWithdrawalAsync(
            new PrivacyWithdrawalRequestInput(ParticipantEmail),
            service,
            rateLimiter,
            clientIpResolver,
            contextAccessor,
            TestContext.Current.CancellationToken);
        var nonmatchingResult = await PrivacyWithdrawalMutations.RequestPrivacyWithdrawalAsync(
            new PrivacyWithdrawalRequestInput("unknown@example.org"),
            service,
            rateLimiter,
            clientIpResolver,
            contextAccessor,
            TestContext.Current.CancellationToken);

        Assert.Equal(matchingResult, nonmatchingResult);
        Assert.True(matchingResult.Acknowledged);
        Assert.Equal(
            "{\"Acknowledged\":true}",
            JsonSerializer.Serialize(matchingResult));
        Assert.Equal(
            [ParticipantEmail, "unknown@example.org"],
            service.RequestedEmails);
    }

    [Fact]
    public async Task RateLimitedRequestStillReturnsTheGenericAcknowledgement()
    {
        var service = new RecordingPrivacyWithdrawalRequestService();
        using var rateLimiter = NewRateLimiter(requestLimit: 1);
        var contextAccessor = NewHttpContextAccessor();
        var clientIpResolver = new FixedClientIpResolver();

        var firstResult = await PrivacyWithdrawalMutations.RequestPrivacyWithdrawalAsync(
            new PrivacyWithdrawalRequestInput(ParticipantEmail),
            service,
            rateLimiter,
            clientIpResolver,
            contextAccessor,
            TestContext.Current.CancellationToken);
        var rateLimitedResult = await PrivacyWithdrawalMutations.RequestPrivacyWithdrawalAsync(
            new PrivacyWithdrawalRequestInput("unknown@example.org"),
            service,
            rateLimiter,
            clientIpResolver,
            contextAccessor,
            TestContext.Current.CancellationToken);

        Assert.Equal(new PrivacyWithdrawalRequestAcknowledgement(Acknowledged: true), firstResult);
        Assert.Equal(firstResult, rateLimitedResult);
        Assert.Single(service.RequestedEmails);
    }

    [Fact]
    public async Task ConfirmationReportsOnlyTheServiceAcceptanceForTheOneTimeKey()
    {
        var service = new RecordingPrivacyWithdrawalRequestService();
        using var rateLimiter = NewRateLimiter(confirmationLimit: 2);
        var contextAccessor = NewHttpContextAccessor();
        var clientIpResolver = new FixedClientIpResolver();

        var validResult = await PrivacyWithdrawalMutations.ConfirmPrivacyWithdrawalAsync(
            ValidKey,
            service,
            rateLimiter,
            clientIpResolver,
            contextAccessor,
            TestContext.Current.CancellationToken);
        var invalidResult = await PrivacyWithdrawalMutations.ConfirmPrivacyWithdrawalAsync(
            "invalid-one-time-key",
            service,
            rateLimiter,
            clientIpResolver,
            contextAccessor,
            TestContext.Current.CancellationToken);

        Assert.True(validResult.Accepted);
        Assert.False(invalidResult.Accepted);
        Assert.Equal("{\"Accepted\":true}", JsonSerializer.Serialize(validResult));
        Assert.Equal([ValidKey, "invalid-one-time-key"], service.ConfirmedKeys);
        Assert.DoesNotContain(ParticipantEmail, JsonSerializer.Serialize(validResult), StringComparison.Ordinal);
        Assert.DoesNotContain(ValidKey, JsonSerializer.Serialize(validResult), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RateLimitedConfirmationIsNotForwardedForProcessing()
    {
        var service = new RecordingPrivacyWithdrawalRequestService();
        using var rateLimiter = NewRateLimiter(confirmationLimit: 1);
        var contextAccessor = NewHttpContextAccessor();
        var clientIpResolver = new FixedClientIpResolver();

        var acceptedResult = await PrivacyWithdrawalMutations.ConfirmPrivacyWithdrawalAsync(
            ValidKey,
            service,
            rateLimiter,
            clientIpResolver,
            contextAccessor,
            TestContext.Current.CancellationToken);
        var rateLimitedResult = await PrivacyWithdrawalMutations.ConfirmPrivacyWithdrawalAsync(
            ValidKey,
            service,
            rateLimiter,
            clientIpResolver,
            contextAccessor,
            TestContext.Current.CancellationToken);

        Assert.True(acceptedResult.Accepted);
        Assert.False(rateLimitedResult.Accepted);
        Assert.Equal([ValidKey], service.ConfirmedKeys);
    }

    [Fact]
    public async Task AnonymousGraphQlMutationsAreMinimalAndConfirmationIsUnavailableToQueries()
    {
        var service = new RecordingPrivacyWithdrawalRequestService();
        using var rateLimiter = NewRateLimiter(requestLimit: 2, confirmationLimit: 2);
        await using var provider = BuildSchemaProvider(service, rateLimiter);
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
        Assert.Equal(
            ["privacyWithdrawalRequestAcknowledgement"],
            FieldNames(Assert.IsAssignableFrom<IComplexTypeDefinition>(
                executor.Schema.Types.GetType<IComplexTypeDefinition>(
                    "RequestPrivacyWithdrawalPayload"))));
        Assert.Equal(
            ["privacyWithdrawalConfirmationResult"],
            FieldNames(Assert.IsAssignableFrom<IComplexTypeDefinition>(
                executor.Schema.Types.GetType<IComplexTypeDefinition>(
                    "ConfirmPrivacyWithdrawalPayload"))));
        Assert.Equal(
            ["acknowledged"],
            FieldNames(Assert.IsAssignableFrom<IComplexTypeDefinition>(
                executor.Schema.Types.GetType<IComplexTypeDefinition>(
                    "PrivacyWithdrawalRequestAcknowledgement"))));
        Assert.Equal(
            ["accepted"],
            FieldNames(Assert.IsAssignableFrom<IComplexTypeDefinition>(
                executor.Schema.Types.GetType<IComplexTypeDefinition>(
                    "PrivacyWithdrawalConfirmationResult"))));

        await using var requestExecution = await executor.ExecuteAsync(
            """
            mutation {
              requestPrivacyWithdrawal(input: { input: { email: "participant@example.org" } }) {
                privacyWithdrawalRequestAcknowledgement {
                  acknowledged
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);
        var requestResult = requestExecution.ExpectOperationResult();
        Assert.Empty(requestResult.Errors);
        Assert.Equal([ParticipantEmail], service.RequestedEmails);
        Assert.NotNull(requestResult.Data);

        await using var confirmationExecution = await executor.ExecuteAsync(
            $$"""
            mutation {
              confirmPrivacyWithdrawal(input: { key: "{{ValidKey}}" }) {
                privacyWithdrawalConfirmationResult {
                  accepted
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);
        var confirmationResult = confirmationExecution.ExpectOperationResult();
        Assert.Empty(confirmationResult.Errors);
        Assert.Equal([ValidKey], service.ConfirmedKeys);
        Assert.NotNull(confirmationResult.Data);

        await using var queryAttemptExecution = await executor.ExecuteAsync(
            $$"""
            query {
              confirmPrivacyWithdrawal(input: { key: "{{ValidKey}}" }) {
                privacyWithdrawalConfirmationResult {
                  accepted
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);
        var queryAttempt = queryAttemptExecution.ExpectOperationResult();
        Assert.NotEmpty(queryAttempt.Errors);
        Assert.Equal([ValidKey], service.ConfirmedKeys);
    }

    private static ServiceProvider BuildSchemaProvider(
        RecordingPrivacyWithdrawalRequestService requestService,
        PersonalDataExportRateLimiter rateLimiter)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<IPrivacyWithdrawalRequestService>(requestService);
        services.AddSingleton<IPersonalDataExportRateLimiter>(rateLimiter);
        services.AddSingleton<IClientIpResolver>(new FixedClientIpResolver());
        services.AddSingleton<IHttpContextAccessor>(NewHttpContextAccessor());
        services
            .AddGraphQLServer()
            .AddQueryType<SchemaQuery>()
            .AddMutationType<SchemaMutation>()
            .AddMutationConventions()
            .AddAuthorization();

        return services.BuildServiceProvider();
    }

    private static string[] FieldNames(IComplexTypeDefinition type) =>
        [.. type.Fields
            .Where(field => !field.IsIntrospectionField)
            .Select(field => field.Name)
            .OrderBy(name => name, StringComparer.Ordinal)];

    private static PersonalDataExportRateLimiter NewRateLimiter(
        int requestLimit = 5,
        int confirmationLimit = 10) =>
        new(new PrivacyChallengeConfiguration
        {
            RateLimitWindowSeconds = 60,
            RequestRateLimitPermitLimit = requestLimit,
            ConfirmationRateLimitPermitLimit = confirmationLimit
        });

    private static IHttpContextAccessor NewHttpContextAccessor() =>
        new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

    private sealed class FixedClientIpResolver : IClientIpResolver
    {
        public string Resolve(HttpContext context) => ClientAddress;
    }

    private sealed class RecordingPrivacyWithdrawalRequestService : IPrivacyWithdrawalRequestService
    {
        public List<string?> RequestedEmails { get; } = [];
        public List<string?> ConfirmedKeys { get; } = [];

        public Task RequestAsync(string? email, CancellationToken cancellationToken)
        {
            RequestedEmails.Add(email);
            return Task.CompletedTask;
        }

        public Task<bool> RequestForParticipantAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> ConfirmAsync(string? challengeKey, CancellationToken cancellationToken)
        {
            ConfirmedKeys.Add(challengeKey);
            return Task.FromResult(challengeKey == ValidKey);
        }
    }

    public sealed class SchemaQuery
    {
        public string Ping() => "pong";
    }

    public sealed class SchemaMutation
    {
        [GraphQLName("requestPrivacyWithdrawal")]
        public Task<PrivacyWithdrawalRequestAcknowledgement> RequestPrivacyWithdrawalAsync(
            PrivacyWithdrawalRequestInput input,
            [Service] IPrivacyWithdrawalRequestService requestService,
            [Service] IPersonalDataExportRateLimiter rateLimiter,
            [Service] IClientIpResolver clientIpResolver,
            [Service] IHttpContextAccessor httpContextAccessor,
            CancellationToken cancellationToken) =>
            PrivacyWithdrawalMutations.RequestPrivacyWithdrawalAsync(
                input,
                requestService,
                rateLimiter,
                clientIpResolver,
                httpContextAccessor,
                cancellationToken);

        [GraphQLName("confirmPrivacyWithdrawal")]
        public Task<PrivacyWithdrawalConfirmationResult> ConfirmPrivacyWithdrawalAsync(
            string key,
            [Service] IPrivacyWithdrawalRequestService requestService,
            [Service] IPersonalDataExportRateLimiter rateLimiter,
            [Service] IClientIpResolver clientIpResolver,
            [Service] IHttpContextAccessor httpContextAccessor,
            CancellationToken cancellationToken) =>
            PrivacyWithdrawalMutations.ConfirmPrivacyWithdrawalAsync(
                key,
                requestService,
                rateLimiter,
                clientIpResolver,
                httpContextAccessor,
                cancellationToken);
    }
}
