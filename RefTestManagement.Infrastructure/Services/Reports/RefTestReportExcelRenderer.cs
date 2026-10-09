using ClosedXML.Excel;
using Handball.Belgium.RefTestManagement.Application.Services;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services.Reports;

/// <summary>Renders the RefTest report workbook: a data sheet with a language switcher backed by a translations sheet.</summary>
internal static class RefTestReportExcelRenderer
{
    public static byte[] Render(List<RefTestReportData> refTestReportDataList, string[] enabledLanguages,
        ITranslationService translationService)
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
            var instructionFormula = BuildLanguageFormula(translationService, enabledLanguages, instructionRow);
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

    private static string BuildLanguageFormula(ITranslationService translationService, string[] enabledLanguages,
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
}
