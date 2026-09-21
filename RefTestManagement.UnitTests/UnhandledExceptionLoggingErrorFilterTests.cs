using Handball.Belgium.RefTestManagement.Api.Graphql;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using HotChocolate;
using HotChocolate.Execution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public class UnhandledExceptionLoggingErrorFilterTests
{
    [Fact]
    public async Task RequestExecutorCanActivateGraphQlErrorFilters()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services
            .AddGraphQLServer()
            .AddQueryType<SchemaQuery>()
            .AddApplicationService<IHttpContextAccessor>()
            .AddApplicationService<ILogger<UnhandledExceptionLoggingErrorFilter>>()
            .AddApplicationService<ILogger<ConcurrencyErrorFilter>>()
            .AddErrorFilter<ConcurrencyErrorFilter>()
            .AddErrorFilter<UnhandledExceptionLoggingErrorFilter>();

        await using var provider = services.BuildServiceProvider();
        var executorProvider = provider.GetRequiredService<IRequestExecutorProvider>();

        var executor = await executorProvider.GetExecutorAsync(
            Assert.Single(executorProvider.SchemaNames),
            TestContext.Current.CancellationToken);

        Assert.NotNull(executor);
    }
    [Fact]
    public void OnErrorMasksEmailAddressesBeforeLoggingTheException()
    {
        using var provider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder
                .AddProvider(provider)
                .SetMinimumLevel(LogLevel.Trace));
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-id" };
        var filter = new UnhandledExceptionLoggingErrorFilter(
            loggerFactory.CreateLogger<UnhandledExceptionLoggingErrorFilter>(),
            new HttpContextAccessor { HttpContext = httpContext });
        var error = ErrorBuilder.New()
            .SetMessage("Unhandled resolver failure")
            .SetException(new InvalidOperationException("failed for john.doe@example.com"))
            .Build();

        filter.OnError(error);

        var loggedException = Assert.Single(provider.Exceptions);
        Assert.NotNull(loggedException);
        Assert.DoesNotContain("john.doe@example.com", loggedException.ToString(), StringComparison.Ordinal);
        Assert.Contains("j***@example.com", loggedException.ToString(), StringComparison.Ordinal);
        Assert.Contains("correlationId=trace-id", Assert.Single(provider.Messages), StringComparison.Ordinal);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<Exception?> Exceptions { get; } = [];
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                provider.Exceptions.Add(exception);
                provider.Messages.Add(formatter(state, exception));
            }
        }
    }

    public sealed class SchemaQuery
    {
        public string Value() => "value";
    }
}

public class MutationErrorHandlingTests
{
    [Fact]
    public void GetUserSafeMessage_PreservesDeliberateValidationMessages()
    {
        const string expected = "A rejection reason is required.";
        var exception = new RefTestValidationException(expected);

        Assert.Equal(expected, MutationErrorHandling.GetUserSafeMessage(exception));
    }

    [Fact]
    public void GetUserSafeMessage_UsesStableMessageForUnexpectedExceptionText()
    {
        const string sentinel = "Sensitive SQL: SELECT * FROM dbo.Users WHERE email = 'john.doe@example.com';";
        var exception = new InvalidOperationException(sentinel);

        var message = MutationErrorHandling.GetUserSafeMessage(exception);

        Assert.Equal(MutationErrorHandling.GenericFailure, message);
        Assert.DoesNotContain(sentinel, message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetUserSafeMessage_UsesNotFoundMessageForNotFoundExceptions()
    {
        var exception = new RefTestNotFoundException(Guid.NewGuid());

        Assert.Equal(MutationErrorHandling.NotFoundFailure, MutationErrorHandling.GetUserSafeMessage(exception));
    }
}
