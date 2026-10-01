using System.Net;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class PersonalDataExportEmailTests
{
    private const string ParticipantEmail = "ada@example.org";
    private static readonly string ChallengeKey = new('B', 43);

    [Fact]
    public async Task ChallengeEmailContainsEveryEnabledLanguageAndLanguageSpecificConfirmationLinks()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.Accepted);
        using var client = new HttpClient(handler);
        var service = CreateService(client, NullLogger<EmailService>.Instance);

        await service.SendPersonalDataExportVerificationAsync(
            ParticipantEmail,
            ChallengeKey,
            DateTime.UtcNow.AddHours(24),
            TestContext.Current.CancellationToken);

        using var providerRequest = JsonDocument.Parse(handler.RequestBody!);
        var root = providerRequest.RootElement;
        var html = root.GetProperty("htmlContent").GetString()!;

        Assert.Equal(
            "Confirm your personal data export request",
            root.GetProperty("subject").GetString());
        foreach (var language in new[] { "en", "nl", "fr", "de" })
        {
            var expectedLink =
                $"https://app.example.org/privacy/export-confirmation?lang={language}#{ChallengeKey}";
            Assert.Contains($"href='{expectedLink}'", html, StringComparison.Ordinal);
        }

        Assert.Contains("Confirm your email address", html, StringComparison.Ordinal);
        Assert.Contains("Bevestig uw e-mailadres", html, StringComparison.Ordinal);
        Assert.Contains("Confirmez votre adresse e-mail", html, StringComparison.Ordinal);
        Assert.Contains(
            System.Web.HttpUtility.HtmlEncode("Bestätigen Sie Ihre E-Mail-Adresse"),
            html,
            StringComparison.Ordinal);
        Assert.Contains("Confirm request", html, StringComparison.Ordinal);
        Assert.Contains("Aanvraag bevestigen", html, StringComparison.Ordinal);
        Assert.Contains("Confirmer la demande", html, StringComparison.Ordinal);
        Assert.Contains(
            System.Web.HttpUtility.HtmlEncode("Anfrage bestätigen"),
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain(ParticipantEmail, html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChallengeEmailProviderFailureDoesNotLogTheRawKeyOrRecipient()
    {
        var providerResponse =
            $"Provider echoed {ChallengeKey} and {ParticipantEmail} from the rejected message.";
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, providerResponse);
        using var client = new HttpClient(handler);
        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
        var service = CreateService(client, loggerFactory.CreateLogger<EmailService>());

        var exception = await Assert.ThrowsAsync<EmailException>(() =>
            service.SendPersonalDataExportVerificationAsync(
                ParticipantEmail,
                ChallengeKey,
                DateTime.UtcNow.AddHours(24),
                TestContext.Current.CancellationToken));

        var logs = string.Join(Environment.NewLine, loggerProvider.Messages);
        Assert.DoesNotContain(ChallengeKey, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(ParticipantEmail, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ChallengeKey, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ParticipantEmail, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeliveryEmailUsesOnlyTheVerifiedRecipientAndAttachesAllNumberedPdfs()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.Accepted);
        using var client = new HttpClient(handler);
        var service = CreateService(client, NullLogger<EmailService>.Instance);
        var attachments = new List<EmailAttachment>
        {
            new("PersonalDataExport_Part_001_of_002.pdf", [1, 2, 3]),
            new("PersonalDataExport_Part_002_of_002.pdf", [4, 5, 6])
        };
        var wasSent = await service.SendPersonalDataExportAsync(
            ParticipantEmail,
            attachments,
            static _ => Task.FromResult(true),
            TestContext.Current.CancellationToken);
        Assert.True(wasSent);

        using var providerRequest = JsonDocument.Parse(handler.RequestBody!);
        var root = providerRequest.RootElement;
        Assert.Equal(
            [ParticipantEmail],
            root.GetProperty("to").EnumerateArray()
                .Select(recipient => recipient.GetProperty("email").GetString())
                .ToArray());
        Assert.Equal(
            "Your personal data export",
            root.GetProperty("subject").GetString());
        var html = root.GetProperty("htmlContent").GetString()!;
        Assert.Contains(
            System.Web.HttpUtility.HtmlEncode("Your RefTest personal data"),
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            System.Web.HttpUtility.HtmlEncode("Uw RefTest-persoonsgegevens"),
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            System.Web.HttpUtility.HtmlEncode("Vos données personnelles RefTest"),
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            System.Web.HttpUtility.HtmlEncode("Ihre RefTest-Daten"),
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            System.Web.HttpUtility.HtmlEncode("Attached is the PDF copy"),
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            System.Web.HttpUtility.HtmlEncode("In de bijlage vindt u de PDF"),
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            System.Web.HttpUtility.HtmlEncode("Vous trouverez en pièce jointe"),
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            System.Web.HttpUtility.HtmlEncode("Im Anhang finden Sie die PDF-Datei"),
            html,
            StringComparison.Ordinal);

        var providerAttachments = root.GetProperty("attachment").EnumerateArray().ToArray();
        Assert.Equal(2, providerAttachments.Length);
        Assert.Equal(attachments[0].FileName, providerAttachments[0].GetProperty("name").GetString());
        Assert.Equal(
            Convert.ToBase64String(attachments[0].Content),
            providerAttachments[0].GetProperty("content").GetString());
        Assert.Equal(attachments[1].FileName, providerAttachments[1].GetProperty("name").GetString());
    }

    [Fact]
    public async Task DeliveryEmailSkipsProviderWhenFinalDeliverabilityCheckFails()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.Accepted);
        using var client = new HttpClient(handler);
        var service = CreateService(client, NullLogger<EmailService>.Instance);
        var finalCheckCount = 0;

        var wasSent = await service.SendPersonalDataExportAsync(
            ParticipantEmail,
            [new EmailAttachment("PersonalDataExport.pdf", [1, 2, 3])],
            _ =>
            {
                finalCheckCount++;
                return Task.FromResult(false);
            },
            TestContext.Current.CancellationToken);

        Assert.False(wasSent);
        Assert.Equal(1, finalCheckCount);
        Assert.Null(handler.RequestBody);
    }

    [Fact]
    public async Task DeliveryEmailPropagatesCancellationDuringFinalDeliverabilityCheck()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.Accepted);
        using var client = new HttpClient(handler);
        var service = CreateService(client, NullLogger<EmailService>.Instance);
        using var cancellationSource = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.SendPersonalDataExportAsync(
                ParticipantEmail,
                [new EmailAttachment("PersonalDataExport.pdf", [1, 2, 3])],
                cancellationToken =>
                {
                    cancellationSource.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(true);
                },
                cancellationSource.Token));

        Assert.Null(handler.RequestBody);
    }

    [Fact]
    public async Task DeliveryEmailRejectsAnAttachmentBeyondProviderLimitsBeforeSending()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.Accepted);
        using var client = new HttpClient(handler);
        var service = CreateService(client, NullLogger<EmailService>.Instance);
        var oversizedAttachment = new EmailAttachment(
            "PersonalDataExport.pdf",
            new byte[PersonalDataExportPdfService.MaxPartBytes + 1]);

        await Assert.ThrowsAsync<PersonalDataExportSizeLimitException>(() =>
            service.SendPersonalDataExportAsync(
                ParticipantEmail,
                [oversizedAttachment],
                static _ => Task.FromResult(true),
                TestContext.Current.CancellationToken));
        Assert.Null(handler.RequestBody);
    }

    [Fact]
    public void VerificationEmailTranslationsAreAvailableForEverySupportedLocale()
    {
        var translations = new TranslationService();
        var englishSubject = translations.GetEmailPersonalDataExportVerificationTranslations("en")["subject"];

        foreach (var locale in new[] { "en", "nl", "fr", "de" })
        {
            var localized = translations.GetEmailPersonalDataExportVerificationTranslations(locale);
            Assert.False(string.IsNullOrWhiteSpace(localized["subject"]));
            Assert.False(string.IsNullOrWhiteSpace(localized["confirmButton"]));
            Assert.Contains("{0}", localized["expiryNote"], StringComparison.Ordinal);
            if (locale != "en")
                Assert.NotEqual(englishSubject, localized["subject"]);
        }
    }

    [Fact]
    public void DeliveryEmailAndPdfTranslationsAreAvailableForEverySupportedLocale()
    {
        var translations = new TranslationService();
        var englishSubject = translations.GetEmailPersonalDataExportDeliveryTranslations("en")["subject"];
        var englishPdfTitle = translations.GetPdfPersonalDataExportTranslations("en")["title"];
        var englishPdfEventType =
            translations.GetPdfPersonalDataExportTranslations("en")["event.RefTestDetailsUpdated"];

        foreach (var locale in new[] { "en", "nl", "fr", "de" })
        {
            var email = translations.GetEmailPersonalDataExportDeliveryTranslations(locale);
            var pdf = translations.GetPdfPersonalDataExportTranslations(locale);

            Assert.False(string.IsNullOrWhiteSpace(email["subject"]));
            Assert.False(string.IsNullOrWhiteSpace(email["heading"]));
            Assert.False(string.IsNullOrWhiteSpace(email["body"]));
            Assert.False(string.IsNullOrWhiteSpace(pdf["title"]));
            Assert.False(string.IsNullOrWhiteSpace(pdf["events"]));
            Assert.False(string.IsNullOrWhiteSpace(pdf["event.RefTestDetailsUpdated"]));
            if (locale != "en")
            {
                Assert.NotEqual(englishSubject, email["subject"]);
                Assert.NotEqual(englishPdfTitle, pdf["title"]);
                Assert.NotEqual(englishPdfEventType, pdf["event.RefTestDetailsUpdated"]);
            }
        }
    }

    private static EmailService CreateService(HttpClient client, ILogger<EmailService> logger)
    {
        var emailConfiguration = new EmailConfiguration
        {
            BaseUrl = "https://app.example.org/",
            BrevoApiKey = "test-api-key",
            BrevoApiUrl = "https://brevo.example.org",
            FromEmail = "noreply@example.org",
            FromName = "RefTest Management"
        };
        var templateService = new EmailTemplateService(new EmptyLogoService());

        return new EmailService(
            logger,
            emailConfiguration,
            LanguageConfiguration.CreateDefault(),
            new ScoreConfiguration(),
            new RefTestExpirationConfiguration(),
            pdfService: null!,
            templateService,
            new TranslationService(),
            client);
    }

    private sealed class EmptyLogoService : ILogoService
    {
        public Task<byte[]?> GetLogoBytesAsync() => Task.FromResult<byte[]?>(null);

        public Task<string> GetLogoAsBase64Async() => Task.FromResult(string.Empty);
    }

    private sealed class RecordingHttpMessageHandler(
        HttpStatusCode statusCode,
        string? responseBody = null) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var response = new HttpResponseMessage(statusCode);
            if (responseBody is not null)
                response.Content = new StringContent(responseBody);
            return response;
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                provider.Messages.Add(formatter(state, exception));
        }
    }
}
