using Handball.Belgium.RefTestManagement.Application.Services;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class LanguageConfigurationTests
{
    [Fact]
    public void ValidateAcceptsSupportedLanguageCodes()
    {
        var configuration = new LanguageConfiguration
        {
            DefaultPhraseLanguage = "en",
            EnabledLanguages = ["en", "nl", "fr", "de"]
        };

        configuration.Validate();
    }

    [Fact]
    public void ValidateRejectsUnsupportedLanguageCodes()
    {
        var configuration = new LanguageConfiguration
        {
            DefaultPhraseLanguage = "en",
            EnabledLanguages = ["xx"]
        };

        var exception = Assert.Throws<InvalidOperationException>(configuration.Validate);

        Assert.Contains("xx", exception.Message);
        Assert.Contains("unsupported language code", exception.Message);
    }
}
