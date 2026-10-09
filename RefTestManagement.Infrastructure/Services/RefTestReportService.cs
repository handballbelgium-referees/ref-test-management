using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Services.Reports;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;


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
        var logo = await logoService.GetLogoBytesAsync(cancellationToken);

        var excelReport = RefTestReportExcelRenderer.Render(refTests, languageConfiguration.EnabledLanguages, translationService);
        var pdfReport = RefTestReportPdfRenderer.Render(refTests, languageConfiguration.EnabledLanguages, logo, translationService);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        foreach (var recipientEmail in recipientEmails)
        {
            await emailService.SendReportEmailAsync(recipientEmail, excelReport, pdfReport, timestamp, refTests.Count, cancellationToken);
        }
    }
}
