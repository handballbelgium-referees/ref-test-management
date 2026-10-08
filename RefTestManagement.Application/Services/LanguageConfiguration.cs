namespace Handball.Belgium.RefTestManagement.Application.Services;

public class LanguageConfiguration
{
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.Ordinal)
    {
        "en",
        "nl",
        "fr",
        "de"
    };

    public required string DefaultPhraseLanguage { get; set; }
    
    public required string[] EnabledLanguages { get; init; }

    public static LanguageConfiguration CreateDefault()
    {
        return new LanguageConfiguration
        {
            DefaultPhraseLanguage = "en",
            EnabledLanguages = ["en", "nl", "fr", "de"]
        };
    }

    public void Validate()
    {
        var unsupportedLanguages = EnabledLanguages
            .Where(language => !SupportedLanguages.Contains(language))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (unsupportedLanguages.Length > 0)
        {
            throw new InvalidOperationException(
                $"LanguageConfiguration:EnabledLanguages contains unsupported language code(s): {string.Join(", ", unsupportedLanguages)}. Supported codes are: {string.Join(", ", SupportedLanguages.Order(StringComparer.Ordinal))}.");
        }
    }

    public void SetDefaultPhraseLanguage(string language) => DefaultPhraseLanguage = language;
}