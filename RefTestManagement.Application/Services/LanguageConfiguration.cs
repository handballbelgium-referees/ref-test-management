namespace Handball.Belgium.RefTestManagement.Application.Services;

public class LanguageConfiguration
{
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

    public void SetDefaultPhraseLanguage(string language) => DefaultPhraseLanguage = language;
}