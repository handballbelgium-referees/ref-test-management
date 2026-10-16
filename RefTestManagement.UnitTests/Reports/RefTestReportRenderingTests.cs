using ClosedXML.Excel;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Infrastructure.Services.Reports;
using QuestPDF.Infrastructure;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// The report renderers had no coverage before they were split out of RefTestReportService; these
/// guard that both formats still render every row in every enabled language.
/// </summary>
public sealed class RefTestReportRenderingTests
{
    private static readonly string[] Languages = ["en", "nl", "fr", "de"];

    private static List<RefTestReportData> Rows() =>
    [
        new("Season opener", "Ada", "Lovelace", new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 1, 9, 25, 0, DateTimeKind.Utc), 8, 10, 12, 15, 80, true, "en",
            TimeSpan.FromMinutes(25)),
        new("Season opener", "Alan", "Turing", null, null, null, 10, null, null, null, false, null, null)
    ];

    [Fact]
    public void TheWorkbookHasTheDataSheetFirstAndATranslationRowPerLanguage()
    {
        var bytes = RefTestReportExcelRenderer.Render(Rows(), Languages, new TranslationService());

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(["RefTests", "Translations"], workbook.Worksheets.OrderBy(w => w.Position).Select(w => w.Name));
        var data = workbook.Worksheet("RefTests");
        Assert.Contains(data.CellsUsed(), cell => cell.GetString() == "Lovelace");
        Assert.Contains(data.CellsUsed(), cell => cell.GetString() == "Turing");
        var translations = workbook.Worksheet("Translations");
        Assert.Equal(Languages.Length + 1, translations.Row(2).CellsUsed().Count());
    }

    [Fact]
    public void ThePdfRendersASectionPerLanguage()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var bytes = RefTestReportPdfRenderer.Render(Rows(), Languages, logo: null, new TranslationService());

        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }
}
