using Handball.Belgium.RefTestManagement.Application.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services.Reports;

/// <summary>Renders the RefTest report PDF: one landscape section per enabled language.</summary>
internal static class RefTestReportPdfRenderer
{
    public static byte[] Render(List<RefTestReportData> refTestReportDataList, string[] enabledLanguages, byte[]? logo,
        ITranslationService translationService)
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
