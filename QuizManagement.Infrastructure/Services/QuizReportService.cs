using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuizManagement.Application.Services;

namespace QuizManagement.Infrastructure.Services;

public class QuizReportService(
    ILogger<QuizReportService> logger,
    IEmailService emailService,
    LanguageConfiguration languageConfiguration)
    : IQuizReportService
{
    public async Task SendReportAsync(List<QuizSessionReportData> sessions, string[] recipientEmails,
        CancellationToken cancellationToken = default)
    {
        if (recipientEmails.Length == 0)
        {
            logger.LogWarning("No recipient emails configured for reports");
            return;
        }

        var excelReport = GenerateExcelReportAsync(sessions, languageConfiguration.EnabledLanguages);
        var pdfReport = GeneratePdfReportAsync(sessions, languageConfiguration.EnabledLanguages);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        foreach (var recipientEmail in recipientEmails)
        {
            await emailService.SendReportEmailAsync(recipientEmail, excelReport, pdfReport, timestamp, sessions.Count);
        }
    }

    private static byte[] GenerateExcelReportAsync(List<QuizSessionReportData> sessions, string[] enabledLanguages)
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
        var languageNames = new Dictionary<string, string>
        {
            ["en"] = "English",
            ["nl"] = "Nederlands",
            ["fr"] = "Français",
            ["de"] = "Deutsch"
        };

        for (var i = 0; i < enabledLanguages.Length; i++)
        {
            var lang = enabledLanguages[i];
            translationsSheet.Cell(2, i + 2).Value =
                languageNames.TryGetValue(lang, out var value) ? value : lang.ToUpper();
        }

        translationsSheet.Range(2, 1, 2, enabledLanguages.Length + 1).Style.Font.Bold = true;
        translationsSheet.Range(2, 1, 2, enabledLanguages.Length + 1).Style.Fill.BackgroundColor = XLColor.LightGray;

        var allTranslations = new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["Title"] = "Title", ["First Name"] = "First Name", ["Last Name"] = "Last Name",
                ["Started At"] = "Started At", ["Completed At"] = "Completed At", ["Duration"] = "Duration",
                ["Language"] = "Language", ["Question Score"] = "Question Score", ["Question Total"] = "Question Total",
                ["Answer Score"] = "Answer Score", ["Answer Total"] = "Answer Total", ["Percentage"] = "Percentage",
                ["Passed"] = "Passed"
            },
            ["nl"] = new()
            {
                ["Title"] = "Titel", ["First Name"] = "Voornaam", ["Last Name"] = "Achternaam",
                ["Started At"] = "Gestart Op", ["Completed At"] = "Voltooid Op", ["Duration"] = "Duur",
                ["Language"] = "Taal", ["Question Score"] = "Vraag Score", ["Question Total"] = "Vraag Totaal",
                ["Answer Score"] = "Antwoord Score", ["Answer Total"] = "Antwoord Totaal",
                ["Percentage"] = "Percentage",
                ["Passed"] = "Geslaagd"
            },
            ["fr"] = new()
            {
                ["Title"] = "Titre", ["First Name"] = "Prénom", ["Last Name"] = "Nom",
                ["Started At"] = "Commencé À", ["Completed At"] = "Terminé À", ["Duration"] = "Durée",
                ["Language"] = "Langue", ["Question Score"] = "Score Questions", ["Question Total"] = "Total Questions",
                ["Answer Score"] = "Score Réponses", ["Answer Total"] = "Total Réponses",
                ["Percentage"] = "Pourcentage",
                ["Passed"] = "Réussi"
            },
            ["de"] = new()
            {
                ["Title"] = "Titel", ["First Name"] = "Vorname", ["Last Name"] = "Nachname",
                ["Started At"] = "Gestartet Am", ["Completed At"] = "Abgeschlossen Am", ["Duration"] = "Dauer",
                ["Language"] = "Sprache", ["Question Score"] = "Fragen Punkte", ["Question Total"] = "Fragen Gesamt",
                ["Answer Score"] = "Antworten Punkte", ["Answer Total"] = "Antworten Gesamt",
                ["Percentage"] = "Prozentsatz",
                ["Passed"] = "Bestanden"
            }
        };

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
                var translation = allTranslations.TryGetValue(lang, out var value1) &&
                                  value1.TryGetValue(key, out var value)
                    ? value
                    : key; // Fallback to an English key if translation not found
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

        // Add compact language switcher in row 1 - all in one merged cell
        var languageLabel = new Dictionary<string, string>
        {
            ["en"] = "Language",
            ["nl"] = "Taal",
            ["fr"] = "Langue",
            ["de"] = "Sprache"
        };

        var instructionTexts = new Dictionary<string, string>
        {
            ["en"] = "Select language",
            ["nl"] = "Selecteer taal",
            ["fr"] = "Sélectionner la langue",
            ["de"] = "Sprache wählen"
        };

        // Add instruction and language label translations to Translations sheet
        var instructionRow = columnKeys.Length + 3;
        var labelRow = columnKeys.Length + 4;
        translationsSheet.Cell(instructionRow, 1).Value = "LanguageInstruction";
        translationsSheet.Cell(labelRow, 1).Value = "LanguageLabel";

        for (var i = 0; i < enabledLanguages.Length; i++)
        {
            var lang = enabledLanguages[i];
            if (instructionTexts.TryGetValue(lang, out var instruction))
            {
                translationsSheet.Cell(instructionRow, i + 2).Value = instruction;
            }

            if (languageLabel.TryGetValue(lang, out var label))
            {
                translationsSheet.Cell(labelRow, i + 2).Value = label;
            }
        }

        // Create compact language switcher in row 1
        var defaultLanguage = languageNames.TryGetValue(enabledLanguages[0], out var value3)
            ? value3
            : enabledLanguages[0].ToUpper();

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
            instructionCell.Value = $"← {instructionTexts[enabledLanguages[0]]}";
        }
        else
        {
            var instructionFormula = BuildLanguageFormula(enabledLanguages, languageNames, instructionRow);
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
                    var langDisplayName = languageNames.TryGetValue(lang, out var value) ? value : lang.ToUpper();
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
        for (var i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            var row = i + 5;

            // Text fields
            worksheet.Cell(row, 1).Value = session.TitleName;
            worksheet.Cell(row, 2).Value = session.FirstName;
            worksheet.Cell(row, 3).Value = session.LastName;

            // DateTime fields - use the proper DateTime type
            if (session.StartedAt.HasValue)
            {
                var startedAtCet = TimeZoneInfo.ConvertTimeFromUtc(session.StartedAt.Value, cetTimeZone);
                worksheet.Cell(row, 4).Value = startedAtCet;
                worksheet.Cell(row, 4).Style.DateFormat.Format = "dd-mm-yyyy hh:mm:ss";
            }

            if (session.CompletedAt.HasValue)
            {
                var completedAtCet = TimeZoneInfo.ConvertTimeFromUtc(session.CompletedAt.Value, cetTimeZone);
                worksheet.Cell(row, 5).Value = completedAtCet;
                worksheet.Cell(row, 5).Style.DateFormat.Format = "dd-mm-yyyy hh:mm:ss";
            }

            // Duration field - format as hh:mm:ss
            if (session.Duration.HasValue)
            {
                var duration = session.Duration.Value;
                var hours = (int)duration.TotalHours;
                var minutes = duration.Minutes;
                var seconds = duration.Seconds;
                worksheet.Cell(row, 6).Value = $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            }

            // Language field
            worksheet.Cell(row, 7).Value = session.Language ?? "";

            // Numeric fields
            if (session.QuestionScore.HasValue)
                worksheet.Cell(row, 8).Value = session.QuestionScore.Value;

            worksheet.Cell(row, 9).Value = session.QuestionTotal;

            if (session.AnswerScore.HasValue)
                worksheet.Cell(row, 10).Value = session.AnswerScore.Value;

            if (session.AnswerTotal.HasValue)
                worksheet.Cell(row, 11).Value = session.AnswerTotal.Value;

            // Percentage as number with custom format
            if (session.Percentage.HasValue)
            {
                worksheet.Cell(row, 12).Value = session.Percentage.Value;
                worksheet.Cell(row, 12).Style.NumberFormat.Format = "0.00 \"%\"";
            }

            // Boolean as text
            worksheet.Cell(row, 13).Value = session.Passed ? "Yes" : "No";
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

    private static string BuildLanguageFormula(string[] enabledLanguages, Dictionary<string, string> languageNames,
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
            var langDisplayName = languageNames.TryGetValue(lang, out var value) ? value : lang.ToUpper();
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

    private static byte[] GeneratePdfReportAsync(List<QuizSessionReportData> sessions, string[] enabledLanguages)
    {
        var languageNames = new Dictionary<string, string>
        {
            ["en"] = "English",
            ["nl"] = "Nederlands",
            ["fr"] = "Français",
            ["de"] = "Deutsch"
        };

        var translations = new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["Report"] = "RefTest Report",
                ["Version"] = "Version",
                ["Title"] = "Title", ["First Name"] = "First Name", ["Last Name"] = "Last Name",
                ["Started At"] = "Started At", ["Completed At"] = "Completed At", ["Duration"] = "Duration",
                ["Language"] = "Language", ["Q Score"] = "Q Score", ["Q Total"] = "Q Total",
                ["A Score"] = "A Score", ["A Total"] = "A Total", ["Percentage"] = "%",
                ["Passed"] = "Passed", ["Yes"] = "Yes", ["No"] = "No",
                ["Generated"] = "Generated", ["Total Sessions"] = "Total Sessions",
                ["Page"] = "Page", ["of"] = "of"
            },
            ["nl"] = new()
            {
                ["Report"] = "RefTest Rapport",
                ["Version"] = "Versie",
                ["Title"] = "Titel", ["First Name"] = "Voornaam", ["Last Name"] = "Achternaam",
                ["Started At"] = "Gestart Op", ["Completed At"] = "Voltooid Op", ["Duration"] = "Duur",
                ["Language"] = "Taal", ["Q Score"] = "Vraag Score", ["Q Total"] = "Vraag Totaal",
                ["A Score"] = "Antwoord Score", ["A Total"] = "Antwoord Totaal", ["Percentage"] = "%",
                ["Passed"] = "Geslaagd", ["Yes"] = "Ja", ["No"] = "Nee",
                ["Generated"] = "Gegenereerd", ["Total Sessions"] = "Totaal Sessies",
                ["Page"] = "Pagina", ["of"] = "van"
            },
            ["fr"] = new()
            {
                ["Report"] = "Rapport RefTest",
                ["Version"] = "Version",
                ["Title"] = "Titre", ["First Name"] = "Prénom", ["Last Name"] = "Nom",
                ["Started At"] = "Commencé", ["Completed At"] = "Terminé", ["Duration"] = "Durée",
                ["Language"] = "Langue", ["Q Score"] = "Score Questions", ["Q Total"] = "Total Questions",
                ["A Score"] = "Score Réponses", ["A Total"] = "Total Réponses", ["Percentage"] = "%",
                ["Passed"] = "Réussi", ["Yes"] = "Oui", ["No"] = "Non",
                ["Generated"] = "Généré", ["Total Sessions"] = "Sessions Totales",
                ["Page"] = "Page", ["of"] = "de"
            },
            ["de"] = new()
            {
                ["Report"] = "RefTest Bericht",
                ["Version"] = "Version",
                ["Title"] = "Titel", ["First Name"] = "Vorname", ["Last Name"] = "Nachname",
                ["Started At"] = "Gestartet", ["Completed At"] = "Abgeschlossen", ["Duration"] = "Dauer",
                ["Language"] = "Sprache", ["Q Score"] = "Fragen Punkte", ["Q Total"] = "Fragen Gesamt",
                ["A Score"] = "Antworten Punkte", ["A Total"] = "Antworten Gesamt", ["Percentage"] = "%",
                ["Passed"] = "Bestanden", ["Yes"] = "Ja", ["No"] = "Nein",
                ["Generated"] = "Erstellt", ["Total Sessions"] = "Gesamt Sitzungen",
                ["Page"] = "Seite", ["of"] = "von"
            }
        };

        var cetTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        var nowCet = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cetTimeZone);

        var document = Document.Create(container =>
        {
            // Generate one section per language
            foreach (var lang in enabledLanguages)
            {
                var langName = languageNames.TryGetValue(lang, out var name) ? name : lang.ToUpper();
                var trans = translations.TryGetValue(lang, out var t) ? t : translations["en"];

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
                                    text.Span($"{trans["Total Sessions"]}: ").SemiBold();
                                    text.Span(sessions.Count.ToString()).FontColor(Colors.Blue.Darken2);
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
                                foreach (var session in sessions)
                                {
                                    var startedAtCet = session.StartedAt.HasValue
                                        ? TimeZoneInfo.ConvertTimeFromUtc(session.StartedAt.Value, cetTimeZone)
                                            .ToString("dd-MM-yyyy HH:mm")
                                        : "-";
                                    var completedAtCet = session.CompletedAt.HasValue
                                        ? TimeZoneInfo.ConvertTimeFromUtc(session.CompletedAt.Value, cetTimeZone)
                                            .ToString("dd-MM-yyyy HH:mm")
                                        : "-";
                                    var durationText = session.Duration.HasValue
                                        ? $"{(int)session.Duration.Value.TotalHours:D2}:{session.Duration.Value.Minutes:D2}:{session.Duration.Value.Seconds:D2}"
                                        : "-";

                                    var passedText = session.Passed ? trans["Yes"] : trans["No"];

                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignLeft().Text(session.TitleName);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignLeft().Text(session.FirstName);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignLeft().Text(session.LastName);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(startedAtCet);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(completedAtCet);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(durationText);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(session.Language?.ToUpper() ?? "-");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(session.QuestionScore?.ToString() ?? "-");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(session.QuestionTotal.ToString());
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(session.AnswerScore?.ToString() ?? "-");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter().Text(session.AnswerTotal?.ToString() ?? "-");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                        .AlignCenter()
                                        .Text(session.Percentage.HasValue ? $"{session.Percentage.Value:F2} %" : "-");
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