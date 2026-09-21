using Handball.Belgium.RefTestManagement.Api.Graphql;
using HotChocolate;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public class UnhandledExceptionLoggingErrorFilterTests
{
    [Fact]
    public void OnErrorMasksEmailAddressesBeforeLoggingTheException()
    {
        using var provider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder
                .AddProvider(provider)
                .SetMinimumLevel(LogLevel.Trace));
        var filter = new UnhandledExceptionLoggingErrorFilter(
            loggerFactory.CreateLogger<UnhandledExceptionLoggingErrorFilter>());
        var error = ErrorBuilder.New()
            .SetMessage("Unhandled resolver failure")
            .SetException(new InvalidOperationException("failed for john.doe@example.com"))
            .Build();

        filter.OnError(error);

        var loggedException = Assert.Single(provider.Exceptions);
        Assert.NotNull(loggedException);
        Assert.DoesNotContain("john.doe@example.com", loggedException.ToString(), StringComparison.Ordinal);
        Assert.Contains("j***@example.com", loggedException.ToString(), StringComparison.Ordinal);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<Exception?> Exceptions { get; } = [];

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
                Func<TState, Exception?, string> formatter) =>
                provider.Exceptions.Add(exception);
        }
    }
}
