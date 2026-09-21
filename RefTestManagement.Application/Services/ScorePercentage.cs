using System.Globalization;

namespace Handball.Belgium.RefTestManagement.Application.Services;

/// <summary>
/// Parses the score percentage returned by the external IHF Rules Questions API.
/// </summary>
/// <remarks>
/// Extracted from <c>IHFRulesQuestionsService.CalculateScoreAsync</c> so it can be tested without
/// a live GraphQL endpoint. The scoring itself is the external service's responsibility; this is
/// the boundary where its answer becomes a number this system stores on a participant's result,
/// which makes it worth pinning.
///
/// The invariant culture is not a detail: the API returns a dot-decimal string, and parsing it
/// under a comma-decimal server locale would read "85.5" as 855 and hand a participant an
/// eight-hundred-percent pass. The parse is therefore explicitly culture-independent, and a value
/// it cannot read is rejected rather than defaulted, because silently scoring zero would look like
/// a failed exam instead of a failed integration.
/// </remarks>
public static class ScorePercentage
{
    /// <summary>
    /// Parses a percentage as returned by the scoring API, accepting both <c>"85.5%"</c> and
    /// <c>"85.5"</c>.
    /// </summary>
    /// <exception cref="FormatException">
    /// The value is null, empty, or not a culture-invariant number.
    /// </exception>
    public static double Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new FormatException("Invalid percentage format");

        var number = value.EndsWith('%') ? value[..^1] : value;

        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage))
            throw new FormatException("Invalid percentage format");

        return percentage;
    }
}
