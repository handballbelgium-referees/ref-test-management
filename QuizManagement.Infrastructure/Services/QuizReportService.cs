using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace QuizManagement.Infrastructure.Services;

public class QuizReportService(
    ILogger<QuizReportService> logger,
    IEmailService emailService)
    : IQuizReportService
{
    public async Task SendReportAsync(List<QuizSessionReportData> sessions, string[] recipientEmails, CancellationToken cancellationToken = default)
    {
        if (recipientEmails.Length == 0)
        {
            logger.LogWarning("No recipient emails configured for reports");
            return;
        }

        var excelReport = GenerateExcelReportAsync(sessions);
        var pdfReport = GeneratePdfReportAsync(sessions);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        
        foreach (var recipientEmail in recipientEmails)
        {
            await emailService.SendReportEmailAsync(recipientEmail, excelReport, pdfReport, timestamp, sessions.Count);
        }
    }

    private static byte[] GenerateExcelReportAsync(List<QuizSessionReportData> sessions)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("RefTests");

        // Add headers
        worksheet.Cell(1, 1).Value = "Title";
        worksheet.Cell(1, 2).Value = "First Name";
        worksheet.Cell(1, 3).Value = "Last Name";
        worksheet.Cell(1, 4).Value = "Started At";
        worksheet.Cell(1, 5).Value = "Completed At";
        worksheet.Cell(1, 6).Value = "Question Score";
        worksheet.Cell(1, 7).Value = "Question Total";
        worksheet.Cell(1, 8).Value = "Answer Score";
        worksheet.Cell(1, 9).Value = "Answer Total";
        worksheet.Cell(1, 10).Value = "Percentage";
        worksheet.Cell(1, 11).Value = "Passed";

        // Style headers
        var headerRange = worksheet.Range(1, 1, 1, 11);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Convert UTC times to Central European Time (Brussels timezone)
        var cetTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");

        // Add data
        for (var i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            var row = i + 2;

            // Text fields
            worksheet.Cell(row, 1).Value = session.TitleName;
            worksheet.Cell(row, 2).Value = session.FirstName;
            worksheet.Cell(row, 3).Value = session.LastName;
            
            // DateTime fields - use proper DateTime type
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
            
            // Numeric fields
            if (session.QuestionScore.HasValue)
                worksheet.Cell(row, 6).Value = session.QuestionScore.Value;
            
            worksheet.Cell(row, 7).Value = session.QuestionTotal;
            
            if (session.AnswerScore.HasValue)
                worksheet.Cell(row, 8).Value = session.AnswerScore.Value;
            
            if (session.AnswerTotal.HasValue)
                worksheet.Cell(row, 9).Value = session.AnswerTotal.Value;
            
            // Percentage as number with custom format
            if (session.Percentage.HasValue)
            {
                worksheet.Cell(row, 10).Value = session.Percentage.Value;
                worksheet.Cell(row, 10).Style.NumberFormat.Format = "0.00 \"%\"";
            }
            
            // Boolean as text
            worksheet.Cell(row, 11).Value = session.Passed ? "Yes" : "No";
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] GeneratePdfReportAsync(List<QuizSessionReportData> sessions)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header()
                    .Height(60)
                    .Background(Colors.Red.Darken3)
                    .Padding(10)
                    .AlignCenter()
                    .Text("RefTest Report")
                    .FontSize(24)
                    .FontColor(Colors.White)
                    .Bold();

                page.Content()
                    .PaddingVertical(10)
                    .Column(column =>
                    {
                        var cetTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
                        var nowCet = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cetTimeZone);
                        column.Item().Text($"Generated: {nowCet:dd-MM-yyyy HH:mm:ss}").FontSize(10);
                        column.Item().PaddingTop(10);

                        column.Item().Table(table =>
                        {
                            // Define columns with fixed widths to prevent wrapping - 11 columns total
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.8f); // Title
                                columns.RelativeColumn(); // First Name
                                columns.RelativeColumn(); // Last Name
                                columns.RelativeColumn(1.8f); // Started At
                                columns.RelativeColumn(1.8f); // Completed At
                                columns.RelativeColumn(0.7f); // Q Score
                                columns.RelativeColumn(0.7f); // Q Total
                                columns.RelativeColumn(0.7f); // A Score
                                columns.RelativeColumn(0.7f); // A Total
                                columns.RelativeColumn(0.9f); // Percentage
                                columns.RelativeColumn(0.7f); // Passed
                            });

                            // Header
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignLeft().Text("Title").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignLeft().Text("First Name").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignLeft().Text("Last Name").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("Started At").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("Completed At").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("Q Score").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("Q Total").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("A Score").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("A Total").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("%").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("Passed").Bold();
                            });

                            // Data rows
                            foreach (var session in sessions)
                            {
                                var startedAtCet = session.StartedAt.HasValue 
                                    ? TimeZoneInfo.ConvertTimeFromUtc(session.StartedAt.Value, cetTimeZone).ToString("dd-MM-yyyy HH:mm") 
                                    : "-";
                                var completedAtCet = session.CompletedAt.HasValue 
                                    ? TimeZoneInfo.ConvertTimeFromUtc(session.CompletedAt.Value, cetTimeZone).ToString("dd-MM-yyyy HH:mm") 
                                    : "-";
                                
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignLeft().Text(session.TitleName);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignLeft().Text(session.FirstName);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignLeft().Text(session.LastName);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(startedAtCet);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(completedAtCet);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(session.QuestionScore?.ToString() ?? "-");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(session.QuestionTotal.ToString());
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(session.AnswerScore?.ToString() ?? "-");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(session.AnswerTotal?.ToString() ?? "-");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(session.Percentage.HasValue ? $"{session.Percentage.Value:F2} %" : "-");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(session.Passed ? "Yes" : "No");
                            }
                        });
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
            });
        });

        return document.GeneratePdf();
    }
}

