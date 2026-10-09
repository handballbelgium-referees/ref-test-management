using Handball.Belgium.RefTestManagement.Api.Configurations;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Startup must reject an out-of-range setting instead of letting a service run with it, and the
/// shipped defaults must pass.
/// </summary>
public sealed class ConfigurationValidationTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void DefaultsPassValidationAndAreRegistered()
    {
        var services = new ServiceCollection();
        var empty = Config();

        services.AddValidatedConfiguration<BackgroundJobConfiguration>(empty, "BackgroundJobConfiguration");
        services.AddValidatedConfiguration<EmailConfiguration>(empty, "EmailConfiguration");
        services.AddValidatedConfiguration<GraphQlLimitsConfiguration>(empty, "GraphQlLimitsConfiguration");
        services.AddValidatedConfiguration<PrivacyChallengeConfiguration>(empty, "PrivacyChallengeConfiguration");
        services.AddValidatedConfiguration<PrivacyConfiguration>(empty, "PrivacyConfiguration", c => c.Validate());
        services.AddValidatedConfiguration<ReportConfiguration>(empty, "ReportConfiguration");
        services.AddValidatedConfiguration<ScoreConfiguration>(empty, "ScoreConfiguration");
        services.AddValidatedConfiguration<RefTestExpirationConfiguration>(empty, "RefTestExpirationConfiguration");
        services.AddValidatedConfiguration<ForwardedHeadersConfiguration>(empty, "ForwardedHeadersConfiguration");

        using var provider = services.BuildServiceProvider();
        Assert.Equal(3, provider.GetRequiredService<BackgroundJobConfiguration>().MaxAttempts);
    }

    [Fact]
    public void BoundValuesAreReturnedAndRegistered()
    {
        var services = new ServiceCollection();

        var value = services.AddValidatedConfiguration<BackgroundJobConfiguration>(
            Config(("BackgroundJobConfiguration:MaxAttempts", "5")), "BackgroundJobConfiguration");

        using var provider = services.BuildServiceProvider();
        Assert.Equal(5, value.MaxAttempts);
        Assert.Same(value, provider.GetRequiredService<BackgroundJobConfiguration>());
    }

    [Theory]
    [InlineData("BackgroundJobConfiguration:MaxAttempts", "0")]
    [InlineData("BackgroundJobConfiguration:PollingIntervalSeconds", "0")]
    [InlineData("BackgroundJobConfiguration:RetainFailedJobsDays", "-1")]
    public void InvalidBackgroundJobSettingsFailNamingTheSection(string key, string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddValidatedConfiguration<BackgroundJobConfiguration>(
                Config((key, value)), "BackgroundJobConfiguration"));

        Assert.StartsWith("BackgroundJobConfiguration", ex.Message);
    }

    [Theory]
    [InlineData("PrivacyChallengeKeyLifetimeHours", "0")]
    [InlineData("PrivacyChallengeKeyLifetimeHours", "169")]
    [InlineData("RateLimitWindowSeconds", "0")]
    [InlineData("RequestRateLimitPermitLimit", "0")]
    [InlineData("ConfirmationRateLimitPermitLimit", "0")]
    [InlineData("CleanupIntervalMinutes", "0")]
    public void InvalidPrivacyChallengeSettingsFail(string property, string value)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddValidatedConfiguration<PrivacyChallengeConfiguration>(
                Config(($"PrivacyChallengeConfiguration:{property}", value)), "PrivacyChallengeConfiguration"));
    }

    [Theory]
    [InlineData("ScoreConfiguration", "PassingPercentage", "101")]
    [InlineData("GraphQlLimitsConfiguration", "RateLimitPermitLimit", "0")]
    [InlineData("RefTestExpirationConfiguration", "ExpirationIfNotStarted", "00:00:00")]
    [InlineData("ForwardedHeadersConfiguration", "ForwardLimit", "0")]
    public void OtherOutOfRangeSettingsFail(string section, string property, string value)
    {
        var configuration = Config(($"{section}:{property}", value));
        var services = new ServiceCollection();

        Action register = section switch
        {
            "ScoreConfiguration" => () => services.AddValidatedConfiguration<ScoreConfiguration>(configuration, section),
            "GraphQlLimitsConfiguration" => () => services.AddValidatedConfiguration<GraphQlLimitsConfiguration>(configuration, section),
            "RefTestExpirationConfiguration" => () => services.AddValidatedConfiguration<RefTestExpirationConfiguration>(configuration, section),
            _ => () => services.AddValidatedConfiguration<ForwardedHeadersConfiguration>(configuration, section)
        };

        Assert.Throws<InvalidOperationException>(register);
    }

    [Fact]
    public void TheCustomValidatorRunsAfterTheAnnotations()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddValidatedConfiguration<PrivacyConfiguration>(
                Config(("PrivacyConfiguration:RetentionYears", "4")), "PrivacyConfiguration", c => c.Validate()));
    }
}
