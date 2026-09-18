using Handball.Belgium.RefTestManagement.Application.Services;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Covers <see cref="ScorePercentage"/>, the boundary where the external scoring API's string
/// answer becomes the number stored on a participant's result.
/// </summary>
public class ScorePercentageTests
{
    [Theory]
    [InlineData("85.5%", 85.5)]
    [InlineData("85.5", 85.5)]
    [InlineData("100%", 100)]
    [InlineData("0", 0)]
    [InlineData("0%", 0)]
    public void ItReadsBothTheSuffixedAndBareForms(string value, double expected) =>
        Assert.Equal(expected, ScorePercentage.Parse(value));

    /// <summary>
    /// The regression this extraction exists to pin. Under a comma-decimal server locale a
    /// culture-sensitive parse reads "85.5" as 855 and reports an eight-hundred-percent pass, so
    /// the parse has to stay explicitly culture-invariant.
    /// </summary>
    [Fact]
    public void ItReadsDotDecimalsRegardlessOfTheServerLocale()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("nl-BE");
            Assert.Equal(85.5, ScorePercentage.Parse("85.5"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    /// <summary>
    /// A comma-decimal value is not something the API sends; accepting it would mean guessing at
    /// which separator was meant, and guessing wrong turns 85,5 into 855.
    /// </summary>
    [Theory]
    [InlineData("85,5")]
    [InlineData("abc")]
    [InlineData("%")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ItRejectsAnythingItCannotReadRatherThanScoringZero(string? value) =>
        Assert.Throws<FormatException>(() => ScorePercentage.Parse(value));
}
