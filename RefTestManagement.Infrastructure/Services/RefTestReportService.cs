using ClosedXML.Excel;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public interface IRefTestReportService
{
    Task SendReportAsync(List<RefTestReportData> refTests, string[] recipientEmails, CancellationToken cancellationToken = default);
}

public record RefTestReportData(
    string TitleName,
    string FirstName,
    string LastName,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int? QuestionScore,
    int QuestionTotal,
    int? AnswerScore,
    int? AnswerTotal,
    double? Percentage,
    bool Passed,
    string? Language,
    TimeSpan? Duration);


public class RefTestReportService(
    ILogger<RefTestReportService> logger,
    IEmailService emailService,
    LanguageConfiguration languageConfiguration,
    ILogoService logoService,
    ITranslationService translationService)
    : IRefTestReportService
{
    public async Task SendReportAsync(List<RefTestReportData> refTests, string[] recipientEmails,
        CancellationToken cancellationToken = default)
    {
        if (recipientEmails.Length == 0)
        {
            ServiceLoggerMessages.LogNoReportRecipients(logger);
            return;
        }

        // Download logo once for all PDFs
        var logo = await logoService.GetLogoBytesAsync();

        var excelReport = GenerateExcelReport(refTests, languageConfiguration.EnabledLanguages);
        var pdfReport = GeneratePdfReport(refTests, languageConfiguration.EnabledLanguages, logo);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        foreach (var recipientEmail in recipientEmails)
        {
            await emailService.SendReportEmailAsync(recipientEmail, excelReport, pdfReport, timestamp, refTests.Count, cancellationToken);
        }
    }

    private byte[] GenerateExcelReport(List<RefTestReportData> refTestReportDataList, string[] enabledLanguages)
    {
        using var workbook = new XLWorkbook();

        // Create translations sheet first
        var translationsSheet = workbook.Worksheets.Add("Translations");
        translationsSheet.Cell(1, 1).Value = "Column Header Translations";
        translationsSheet.Range(1, 1, 1, enabledLanguages.Length + 1).Merge();
        translationsSheet.Cell(1, 1).Style.Font.Bold = true;
        translationsSheet.Cell(1, 1).Style.Font.FontSize = 14;
        translationsSheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0, 112, 192);
        translationsSheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        translationsSheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Add translation headers
        translationsSheet.Cell(2, 1).Value = "Column";

        for (var i = 0; i < enabledLanguages.Length; i++)
        {
            var lang = enabledLanguages[i];
            translationsSheet.Cell(2, i + 2).Value = translationService.GetLanguageDisplayName(lang);
        }

        translationsSheet.Range(2, 1, 2, enabledLanguages.Length + 1).Style.Font.Bold = true;
        translationsSheet.Range(2, 1, 2, enabledLanguages.Length + 1).Style.Fill.BackgroundColor = XLColor.LightGray;


        var columnKeys = new[]
        {
            "Title", "First Name", "Last Name", "Started At", "Completed At", "Duration",
            "Language", "Question Score", "Question Total", "Answer Score", "Answer Total",
            "Percentage", "Passed"
        };

        for (var i = 0; i < columnKeys.Length; i++)
        {
            var row = i + 3;
            var key = columnKeys[i];
            translationsSheet.Cell(row, 1).Value = key;

            for (var langIdx = 0; langIdx < enabledLanguages.Length; langIdx++)
            {
                var lang = enabledLanguages[langIdx];
                var translations = translationService.GetReportColumnTranslations(lang);
                var translation = translations.GetValueOrDefault(key, key);
                translationsSheet.Cell(row, langIdx + 2).Value = translation;
            }

            if (i % 2 == 0)
            {
                translationsSheet.Range(row, 1, row, enabledLanguages.Length + 1).Style.Fill.BackgroundColor =
                    XLColor.FromArgb(242, 242, 242);
            }
        }

        translationsSheet.Columns().AdjustToContents();

        // Create the main data sheet
        var worksheet = workbook.Worksheets.Add("RefTests");
        worksheet.Position = 1; // Make it the first sheet

        // Add instruction and language label translations to a Translations sheet
        var instructionRow = columnKeys.Length + 3;
        var labelRow = columnKeys.Length + 4;
        translationsSheet.Cell(instructionRow, 1).Value = "LanguageInstruction";
        translationsSheet.Cell(labelRow, 1).Value = "Language";

        for (var i = 0; i < enabledLanguages.Length; i++)
        {
            var lang = enabledLanguages[i];
            var translations = translationService.GetReportColumnTranslations(lang);
            translationsSheet.Cell(instructionRow, i + 2).Value = translations.GetValueOrDefault("LanguageInstruction", "Select language");
            translationsSheet.Cell(labelRow, i + 2).Value = translations.GetValueOrDefault("Language", "Language");
        }

        // Create a compact language switcher in row 1
        var defaultLanguage = translationService.GetLanguageDisplayName(enabledLanguages[0]);

        // A1: Label
        worksheet.Cell(1, 1).Value = "Language:";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(68, 114, 196);
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        worksheet.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // B1: Dropdown with language selection
        worksheet.Cell(1, 2).Value = defaultLanguage;
        worksheet.Cell(1, 2).Style.Font.Bold = true;
        worksheet.Cell(1, 2).Style.Fill.BackgroundColor = XLColor.White;
        worksheet.Cell(1, 2).Style.Font.FontColor = XLColor.FromArgb(68, 114, 196);
        worksheet.Cell(1, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Cell(1, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Cell(1, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        worksheet.Cell(1, 2).Style.Border.OutsideBorderColor = XLColor.FromArgb(68, 114, 196);

        // Add data validation dropdown to B1
        var validation = worksheet.Cell(1, 2).CreateDataValidation();
        var firstLangCol = GetExcelColumnName(2);
        var lastLangCol = GetExcelColumnName(enabledLanguages.Length + 1);
        validation.List($"Translations!${firstLangCol}$2:${lastLangCol}$2", true);
        validation.IgnoreBlanks = true;
        validation.InCellDropdown = true;

        // C1-M1: Dynamic instruction
        var instructionCell = worksheet.Cell(1, 3);
        if (enabledLanguages.Length == 1)
        {
            var firstLangTranslations = translationService.GetReportColumnTranslations(enabledLanguages[0]);
            instructionCell.Value = $"← {firstLangTranslations.GetValueOrDefault("LanguageInstruction", "Select language")}";
        }
        else
        {
            var instructionFormula = BuildLanguageFormula(enabledLanguages, instructionRow);
            instructionCell.FormulaA1 = $@"=""← ""&{instructionFormula}";
        }

        instructionCell.Style.Font.Italic = true;
        instructionCell.Style.Font.FontSize = 9;
        instructionCell.Style.Font.FontColor = XLColor.FromArgb(100, 100, 100);
        worksheet.Range(1, 3, 1, 13).Merge();

        // Rows 2 and 3 are left empty for spacing

        // Add dynamic headers (row 4) that change based on language selection
        for (var col = 1; col <= 13; col++)
        {
            var cell = worksheet.Cell(4, col);

            // Build a nested IF formula for each enabled language
            if (enabledLanguages.Length == 1)
            {
                // Simple case: only one language
                var columnRef = GetExcelColumnName(2); // Column B
                cell.FormulaA1 = $"=Translations!{columnRef}{col + 2}";
            }
            else
            {
                // Build nested IF statements
                var formula = "";
                for (var langIdx = 0; langIdx < enabledLanguages.Length; langIdx++)
                {
                    var lang = enabledLanguages[langIdx];
                    var langDisplayName = translationService.GetLanguageDisplayName(lang);
                    var columnRef = GetExcelColumnName(langIdx + 2); // Column B=2, C=3, D=4, etc.

                    if (langIdx == 0)
                    {
                        formula = $@"IF($B$1=""{langDisplayName}"",Translations!{columnRef}{col + 2}";
                    }
                    else if (langIdx == enabledLanguages.Length - 1)
                    {
                        // Last language - close with the final value
                        formula +=
                            $@",IF($B$1=""{langDisplayName}"",Translations!{columnRef}{col + 2},Translations!B{col + 2}))";
                    }
                    else
                    {
                        formula += $@",IF($B$1=""{langDisplayName}"",Translations!{columnRef}{col + 2}";
                    }
                }

                // Add closing parentheses for all but the last IF (which already closed)
                var closingParens = new string(')', enabledLanguages.Length - 2);
                cell.FormulaA1 = $"={formula}{closingParens}";
            }
        }

        // Style headers
        var headerRange = worksheet.Range(4, 1, 4, 13);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // Convert UTC times to Central European Time (Brussels timezone)
        var cetTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");

        // Add data
        for (var i = 0; i < refTestReportDataList.Count; i++)
        {
            var refTestReportData = refTestReportDataList[i];
            var row = i + 5;

            // Text fields
            worksheet.Cell(row, 1).Value = refTestReportData.TitleName;
            worksheet.Cell(row, 2).Value = refTestReportData.FirstName;
            worksheet.Cell(row, 3).Value = refTestReportData.LastName;

            // DateTime fields - use the proper DateTime type
            if (refTestReportData.StartedAt.HasValue)
            {
                var startedAtCet = TimeZoneInfo.ConvertTimeFromUtc(refTestReportData.StartedAt.Value, cetTimeZone);
                worksheet.Cell(row, 4).Value = startedAtCet;
                worksheet.Cell(row, 4).Style.DateFormat.Format = "dd-mm-yyyy hh:mm:ss";
            }

            if (refTestReportData.CompletedAt.HasValue)
            {
                var completedAtCet = TimeZoneInfo.ConvertTimeFromUtc(refTestReportData.CompletedAt.Value, cetTimeZone);
                worksheet.Cell(row, 5).Value = completedAtCet;
                worksheet.Cell(row, 5).Style.DateFormat.Format = "dd-mm-yyyy hh:mm:ss";
            }

            // Duration field - format as hh:mm:ss
            if (refTestReportData.Duration.HasValue)
            {
                var duration = refTestReportData.Duration.Value;
                var hours = (int)duration.TotalHours;
                var minutes = duration.Minutes;
                var seconds = duration.Seconds;
                worksheet.Cell(row, 6).Value = $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            }

            // Language field
            worksheet.Cell(row, 7).Value = refTestReportData.Language ?? "";

            // Numeric fields
            if (refTestReportData.QuestionScore.HasValue)
                worksheet.Cell(row, 8).Value = refTestReportData.QuestionScore.Value;

            worksheet.Cell(row, 9).Value = refTestReportData.QuestionTotal;

            if (refTestReportData.AnswerScore.HasValue)
                worksheet.Cell(row, 10).Value = refTestReportData.AnswerScore.Value;

            if (refTestReportData.AnswerTotal.HasValue)
                worksheet.Cell(row, 11).Value = refTestReportData.AnswerTotal.Value;

            // Percentage as number with custom format
            if (refTestReportData.Percentage.HasValue)
            {
                worksheet.Cell(row, 12).Value = refTestReportData.Percentage.Value;
                worksheet.Cell(row, 12).Style.NumberFormat.Format = "0.00 \"%\"";
            }

            // Boolean as text
            worksheet.Cell(row, 13).Value = refTestReportData.Passed ? "Yes" : "No";
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string GetExcelColumnName(int columnNumber)
    {
        var columnName = "";
        while (columnNumber > 0)
        {
            var modulo = (columnNumber - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            columnNumber = (columnNumber - modulo) / 26;
        }

        return columnName;
    }

    private string BuildLanguageFormula(string[] enabledLanguages,
        int row, string cellRef = "B")
    {
        if (enabledLanguages.Length == 1)
        {
            var columnRef = GetExcelColumnName(2);
            return $"Translations!{columnRef}{row}";
        }

        var formula = "";
        for (var langIdx = 0; langIdx < enabledLanguages.Length; langIdx++)
        {
            var lang = enabledLanguages[langIdx];
            var langDisplayName = translationService.GetLanguageDisplayName(lang);
            var columnRef = GetExcelColumnName(langIdx + 2);

            if (langIdx == 0)
            {
                formula = $@"IF(${cellRef}$1=""{langDisplayName}"",Translations!{columnRef}{row}";
            }
            else if (langIdx == enabledLanguages.Length - 1)
            {
                formula +=
                    $@",IF(${cellRef}$1=""{langDisplayName}"",Translations!{columnRef}{row},Translations!B{row}))";
            }
            else
            {
                formula += $@",IF(${cellRef}$1=""{langDisplayName}"",Translations!{columnRef}{row}";
            }
        }

        var closingParens = new string(')', Math.Max(0, enabledLanguages.Length - 2));
        return $"{formula}{closingParens}";
    }

    private byte[] GeneratePdfReport(List<RefTestReportData> refTestReportDataList, string[] enabledLanguages, byte[]? logo)
    {
        var cetTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        var nowCet = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cetTimeZone);

        var document = Document.Create(container =>
        {
            // Generate one section per language
            foreach (var lang in enabledLanguages)
            {
                var langName = translationService.GetLanguageDisplayName(lang);
                var trans = translationService.GetPdfReportTranslations(lang);

                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header()
                        .Background(Colors.Red.Darken3)
                        .Border(2)
                        .BorderColor(Colors.Red.Darken4)
                        .Padding(15)
                        .Row(row =>
                        {
                            // Add logo if available
                            if (logo != null)
                            {
                                row.ConstantItem(60).AlignMiddle().Height(50).Image(logo);
                                row.ConstantItem(15); // Spacing between logo and text
                            }

                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text(trans["Report"]).FontSize(24).FontColor(Colors.White).Bold();
                                col.Item().Text($"{langName} {trans["Version"]}").FontSize(10).FontColor(Colors.White)
                                    .Light();
                            });

                            row.ConstantItem(120).AlignRight().Column(col =>
                            {
                                col.Item().Text($"{nowCet:dd-MM-yyyy}").FontSize(10).FontColor(Colors.White);
                                col.Item().Text($"{nowCet:HH:mm:ss}").FontSize(9).FontColor(Colors.White).Light();
                            });
                        });

                    page.Content()
                        .PaddingVertical(10)
                        .Column(column =>
                        {
                            // Summary info box
                            column.Item().Background(Colors.Blue.Lighten4).Padding(8).Row(row =>
                            {
                                row.RelativeItem().Text(text =>
                                {
                                    text.DefaultTextStyle(x => x.FontSize(9));
                                    text.Span($"{trans["Generated"]}: ").SemiBold();
                                    text.Span($"{nowCet:dd-MM-yyyy HH:mm:ss}");
                                });

                                row.RelativeItem().AlignRight().Text(text =>
                                {
                                    text.DefaultTextStyle(x => x.FontSize(9));
                                    text.Span($"{trans["Total RefTests"]}: ").SemiBold();
                                    text.Span(refTestReportDataList.Count.ToString()).FontColor(Colors.Blue.Darken2);
                                });
                            });

                            column.Item().PaddingTop(10);

                            column.Item().Table(table =>
                            {
                                // Define columns with fixed widths
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(0.8f);
                                    columns.RelativeColumn(0.6f);
                                    columns.RelativeColumn(0.7f);
                                    columns.RelativeColumn(0.7f);
                                    columns.RelativeColumn(0.7f);
                                    columns.RelativeColumn(0.7f);
                                    columns.RelativeColumn(0.8f);
                                    columns.RelativeColumn(0.6f);
                                });

                                // Translated headers - these will automatically repeat on each page
                                table.Header(header =>
                                {
                                    HeaderCell(trans["Title"], true);
                                    HeaderCell(trans["First Name"], true);
                                    HeaderCell(trans["Last Name"], true);
                                    HeaderCell(trans["Started At"]);
                                    HeaderCell(trans["Completed At"]);
                                    HeaderCell(trans["Duration"]);
                                    HeaderCell(trans["Language"]);
                                    HeaderCell(trans["Q Score"]);
                                    HeaderCell(trans["Q Total"]);
                                    HeaderCell(trans["A Score"]);
                                    HeaderCell(trans["A Total"]);
                                    HeaderCell(trans["Percentage"]);
                                    HeaderCell(trans["Passed"]);
                                    return;

                                    void HeaderCell(string text, bool alignLeft = false)
                                    {
                                        var cell = header.Cell().Background(Colors.Grey.Lighten2).Padding(5);
                                        if (alignLeft)
                                            cell.AlignLeft().Text(text).FontSize(8).Bold();
                                        else
                                            cell.AlignCenter().Text(text).FontSize(8).Bold();
                                    }
                                });

                                // Data rows
                                foreach (var refTestReportData in refTestReportDataList)
                                {
                                    var startedAtCet = refTestReportData.StartedAt.HasValue
                                        ? TimeZoneInfo.ConvertTimeFromUtc(refTestReportData.StartedAt.Value, cetTimeZone)
                                            .ToString("dd-MM-yyyy HH:mm")
                                        : "-";
                                    var completedAtCet = refTestReportData.CompletedAt.HasValue
                                        ? TimeZoneInfo.ConvertTimeFromUtc(refTestReportData.CompletedAt.Value, cetTimeZone)
                                            .ToString("dd-MM-yyyy HH:mm")
                                        : "-";
                                    var durationText = refTestReportData.Duration.HasValue
                                        ? $"{(int)refTestReportData.Duration.Value.TotalHours:D2}:{refTestReportData.Duration.Value.Minutes:D2}:{refTestReportData.Duration.Value.Seconds:D2}"
                                        : "-";

                                    var passedText = refTestReportData.Passed ? trans["Yes"] : trans["No"];

                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignLeft().Text(refTestReportData.TitleName);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignLeft().Text(refTestReportData.FirstName);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignLeft().Text(refTestReportData.LastName);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(startedAtCet);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(completedAtCet);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(durationText);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(refTestReportData.Language?.ToUpper() ?? "-");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(refTestReportData.QuestionScore?.ToString() ?? "-");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(refTestReportData.QuestionTotal.ToString());
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(refTestReportData.AnswerScore?.ToString() ?? "-");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(refTestReportData.AnswerTotal?.ToString() ?? "-");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter()
                                        .Text(refTestReportData.Percentage.HasValue ? $"{refTestReportData.Percentage.Value:F2} %" : "-");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(passedText);
                                }
                            });
                        });

                    page.Footer()
                        .BorderTop(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .PaddingTop(10)
                        .Row(row =>
                        {
                            row.RelativeItem().AlignLeft().Text("Referees Handball Belgium").FontSize(8)
                                .FontColor(Colors.Grey.Darken1);
                            row.RelativeItem().AlignCenter().Text(text =>
                            {
                                text.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                                text.Span($"{trans["Page"]} ");
                                text.CurrentPageNumber();
                                text.Span($" {trans["of"]} ");
                                text.TotalPages();
                                text.Span($" ({langName})");
                            });
                            row.RelativeItem().AlignRight().Text(langName).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                });
            }
        });

        return document.GeneratePdf();
    }
}