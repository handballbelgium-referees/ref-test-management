using Handball.Belgium.RefTestManagement.Api.Identity;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using HotChocolate.Caching;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Queries;

/// <summary>
/// Public configuration queries used to initialize the application UI.
/// </summary>
[QueryType]
public static partial class ConfigurationQueries
{
    /// <summary>
    /// Gets the enabled languages from configuration.
    /// </summary>
    /// <param name="languageConfiguration">The language configuration containing enabled languages.</param>
    /// <returns>An array of enabled languages.</returns>
    [CacheControl(MaxAge = 3600)]
    public static string[] GetEnabledLanguages([Service] LanguageConfiguration languageConfiguration)
        => languageConfiguration.EnabledLanguages;

    /// <summary>
    /// Gets the score configuration.
    /// </summary>
    /// <param name="scoreConfiguration">The score configuration containing score settings.</param>
    /// <returns>The score configuration.</returns>
    [CacheControl(MaxAge = 3600)]
    public static ScoreConfiguration GetScoreConfiguration([Service] ScoreConfiguration scoreConfiguration)
        => scoreConfiguration;

    /// <summary>
    /// Gets the results mail scheduled delay in minutes.
    /// </summary>
    /// <param name="emailConfiguration">The email configuration containing scheduled delay settings.</param>
    /// <returns>The scheduled delay minutes for results email.</returns>
    [CacheControl(MaxAge = 3600)]
    public static int GetResultsEmailDelayMinutes([Service] EmailConfiguration emailConfiguration)
        => emailConfiguration.ScheduledDelayMinutes;

    /// <summary>
    /// Gets the configured identity provider for public sign-in information.
    /// </summary>
    [CacheControl(MaxAge = 3600)]
    public static string GetIdentityProvider([Service] IConfiguration configuration)
    {
        var provider = IdentityProviderSelector.Resolve(configuration);
        return IdentityProviderSelector.SchemeName(provider);
    }
}
