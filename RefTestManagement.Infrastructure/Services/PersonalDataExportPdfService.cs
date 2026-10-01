using System.Globalization;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.AuditLog;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

/// <summary>Builds localized, in-memory PDF attachments for a verified data export.</summary>
public interface IPersonalDataExportPdfService
{
    IReadOnlyList<EmailAttachment> GenerateAttachments(PersonalDataExportDocumentData document);
}

/// <summary>
/// Renders participant-associated RefTest fields and retained audit events. Oversized documents
/// are divided at RefTest/event boundaries into numbered, valid PDFs; no content is truncated.
/// </summary>
public sealed class PersonalDataExportPdfService(
    ITranslationService translationService,
    LanguageConfiguration languageConfiguration,
    int maxPartBytes = 8 * 1024 * 1024) : IPersonalDataExportPdfService
{
    /// <summary>
    /// Brevo's transactional email request limit is 20 MiB. Keep an individual PDF part below
    /// half that size so the base64-encoded attachment and request envelope remain comfortably
    /// below the provider limit.
    /// </summary>
    public const int MaxPartBytes = 8 * 1024 * 1024;

    public IReadOnlyList<EmailAttachment> GenerateAttachments(PersonalDataExportDocumentData document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.RefTests.Count == 0)
            throw new InvalidOperationException("No current RefTest records are available for export.");
        if (maxPartBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPartBytes));

        var eventsByStream = document.AuditEvents
            .GroupBy(auditEvent => auditEvent.StreamId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PersonalDataExportAuditEventData>)group
                .OrderBy(auditEvent => auditEvent.Version)
                .ThenBy(auditEvent => auditEvent.Timestamp)
                .ToList(), StringComparer.Ordinal);

        var sections = document.RefTests
            .OrderBy(refTest => refTest.CreatedAt)
            .ThenBy(refTest => refTest.Id)
            .Select(refTest => new PdfSection(
                refTest,
                eventsByStream.GetValueOrDefault(refTest.Id.ToString()) ?? [],
                IncludeStoredFields: true,
                IsContinuation: false))
            .ToList();

        var pendingSections = new Queue<List<PdfSection>>();
        var generatedSections = new List<List<PdfSection>>();
        pendingSections.Enqueue(sections);

        while (pendingSections.TryDequeue(out var currentSections))
        {
            // The placeholder uses the longest practical part number so the final numbered
            // heading cannot grow beyond the size measured here.
            var candidate = GeneratePdf(
                document,
                currentSections,
                int.MaxValue,
                int.MaxValue);

            if (candidate.Length <= maxPartBytes)
            {
                generatedSections.Add(currentSections);
                continue;
            }

            if (!TrySplit(currentSections, out var firstHalf, out var secondHalf))
                throw new PersonalDataExportSizeLimitException();

            pendingSections.Enqueue(firstHalf);
            pendingSections.Enqueue(secondHalf);
        }

        var attachments = new List<EmailAttachment>(generatedSections.Count);
        for (var index = 0; index < generatedSections.Count; index++)
        {
            var partNumber = index + 1;
            var content = GeneratePdf(
                document,
                generatedSections[index],
                partNumber,
                generatedSections.Count);

            if (content.Length > maxPartBytes)
                throw new PersonalDataExportSizeLimitException();

            var fileName = generatedSections.Count == 1
                ? "PersonalDataExport.pdf"
                : $"PersonalDataExport_Part_{partNumber:D3}_of_{generatedSections.Count:D3}.pdf";
            attachments.Add(new EmailAttachment(fileName, content));
        }

        return attachments;
    }

    private static bool TrySplit(
        List<PdfSection> sections,
        out List<PdfSection> firstHalf,
        out List<PdfSection> secondHalf)
    {
        if (sections.Count > 1)
        {
            var midpoint = sections.Count / 2;
            firstHalf = sections[..midpoint];
            secondHalf = sections[midpoint..];
            return true;
        }

        if (sections.Count == 1 && sections[0].AuditEvents.Count > 1)
        {
            var section = sections[0];
            var midpoint = section.AuditEvents.Count / 2;
            firstHalf =
            [
                section with { AuditEvents = section.AuditEvents.Take(midpoint).ToList() }
            ];
            secondHalf =
            [
                section with
                {
                    AuditEvents = section.AuditEvents.Skip(midpoint).ToList(),
                    IncludeStoredFields = false,
                    IsContinuation = true
                }
            ];
            return true;
        }

        firstHalf = [];
        secondHalf = [];
        return false;
    }

    private byte[] GeneratePdf(
        PersonalDataExportDocumentData document,
        IReadOnlyList<PdfSection> sections,
        int partNumber,
        int partCount)
    {
        var pdf = Document.Create(container =>
        {
            foreach (var language in languageConfiguration.EnabledLanguages)
            {
                var translations = translationService.GetPdfPersonalDataExportTranslations(language);
                var culture = GetCulture(language);

                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(text => text.FontSize(9));

                    page.Content().Column(column =>
                    {
                        column.Item().PaddingBottom(6).Text(translations["title"])
                            .FontSize(20)
                            .Bold();
                        column.Item().PaddingBottom(2).Text(
                            translationService.GetLanguageDisplayName(language))
                            .FontSize(10)
                            .Italic();
                        column.Item().PaddingBottom(10).Element(item => AddField(
                            item,
                            translations["recipientEmail"],
                            document.RecipientEmail));

                        if (partCount > 1)
                        {
                            var partText = string.Format(
                                culture,
                                translations["part"],
                                partNumber,
                                partCount);
                            column.Item().PaddingBottom(10).Text(partText).Italic();
                        }

                        foreach (var section in sections)
                            column.Item().PaddingBottom(12).Element(item =>
                                ComposeRefTest(item, section, translations, culture));
                    });

                    page.Footer().AlignCenter().Text(translations["footer"]);
                });
            }
        });

        return pdf.GeneratePdf();
    }

    private static void ComposeRefTest(
        IContainer container,
        PdfSection section,
        IReadOnlyDictionary<string, string> translations,
        CultureInfo culture)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(4).Text(text =>
            {
                text.Span(translations["refTest"]).Bold();
                if (section.IsContinuation)
                    text.Span($" — {translations["continued"]}").Italic();
            });

            if (section.IncludeStoredFields)
                AddRefTestFields(column, section.RefTest, translations, culture);

            if (section.AuditEvents.Count == 0)
                return;

            column.Item().PaddingTop(6).PaddingBottom(3).Text(translations["events"]).Bold();
            foreach (var auditEvent in section.AuditEvents)
                column.Item().PaddingBottom(8).Element(item =>
                    ComposeAuditEvent(item, auditEvent, translations, culture));
        });
    }

    private static void AddRefTestFields(
        ColumnDescriptor column,
        PersonalDataExportRefTestData refTest,
        IReadOnlyDictionary<string, string> translations,
        CultureInfo culture)
    {
        AddField(column, translations["firstName"], refTest.FirstName);
        AddField(column, translations["lastName"], refTest.LastName);
        AddField(column, translations["email"], refTest.Email);
        AddField(column, translations["numberOfQuestions"], refTest.NumberOfQuestions.ToString(culture));
        AddField(column, translations["maxTimeInMinutes"], refTest.MaxTimeInMinutes.ToString(culture));
        AddField(column, translations["createdAt"], FormatDate(refTest.CreatedAt, culture, translations));
        AddField(column, translations["startedAt"], FormatDate(refTest.StartedAt, culture, translations));
        AddField(column, translations["completedAt"], FormatDate(refTest.CompletedAt, culture, translations));
        AddField(column, translations["expiredAt"], FormatDate(refTest.ExpiredAt, culture, translations));
        AddField(column, translations["questionScore"],
            FormatNumber(refTest.QuestionScore, culture, translations));
        AddField(column, translations["answerScore"], FormatNumber(refTest.AnswerScore, culture, translations));
        AddField(column, translations["answerTotal"], FormatNumber(refTest.AnswerTotal, culture, translations));
        AddField(column, translations["percentage"],
            refTest.Percentage?.ToString("F2", culture) ?? translations["notRecorded"]);
        AddField(column, translations["language"], refTest.Language ?? translations["notRecorded"]);
        AddField(column, translations["privacyNoticeVersion"],
            refTest.PrivacyNoticeVersion ?? translations["notRecorded"]);
        AddField(column, translations["privacyNoticeAcceptedAt"],
            FormatDate(refTest.PrivacyNoticeAcceptedAt, culture, translations));
        AddField(column, translations["scheduledAt"], FormatDate(refTest.ScheduledAt, culture, translations));
    }

    private static void ComposeAuditEvent(
        IContainer container,
        PersonalDataExportAuditEventData auditEvent,
        IReadOnlyDictionary<string, string> translations,
        CultureInfo culture)
    {
        container.Column(column =>
        {
            AddField(column, translations["version"], auditEvent.Version.ToString(culture));
            AddField(column, translations["eventType"], LocalizeEventType(auditEvent.Type, translations));
            AddField(column, translations["timestamp"], FormatDate(auditEvent.Timestamp, culture, translations));
            AddField(column, translations["actor"], LocalizeActor(auditEvent, translations));
            if (!string.IsNullOrEmpty(auditEvent.ActorEmail))
                AddField(column, translations["actorEmail"], auditEvent.ActorEmail);
            AddField(column, translations["archived"],
                FormatBoolean(auditEvent.IsArchived, translations));
            AddField(column, translations["redactedAt"],
                FormatDate(auditEvent.RedactedAt, culture, translations));
            AddField(column, translations["details"], FormatJson(auditEvent.Data, translations));
        });
    }

    private static void AddField(IContainer container, string label, string value)
    {
        container.Row(row =>
        {
            row.ConstantItem(180).Text(label).Bold();
            row.RelativeItem().Text(value);
        });
    }

    private static void AddField(ColumnDescriptor column, string label, string value) =>
        column.Item().Element(container => AddField(container, label, value));

    private static string LocalizeEventType(
        string eventType,
        IReadOnlyDictionary<string, string> translations) =>
        translations.TryGetValue($"event.{eventType}", out var localizedEventType)
            ? localizedEventType
            : eventType;

    private static string LocalizeActor(
        PersonalDataExportAuditEventData auditEvent,
        IReadOnlyDictionary<string, string> translations) =>
        auditEvent.ActorKind switch
        {
            PersonalDataExportActorKind.Participant => auditEvent.ActorName,
            PersonalDataExportActorKind.VerifiedParticipant => translations["actorVerifiedParticipant"],
            PersonalDataExportActorKind.System => translations["actorSystem"],
            PersonalDataExportActorKind.Staff => translations["actorStaff"],
            PersonalDataExportActorKind.Redacted => AuditPiiRedactor.RedactedValue,
            _ => translations["actorOther"]
        };

    private static string FormatDate(
        DateTime? value,
        CultureInfo culture,
        IReadOnlyDictionary<string, string> translations) =>
        value is { } date
            ? $"{DateTime.SpecifyKind(date, DateTimeKind.Utc).ToString("g", culture)} UTC"
            : translations["notRecorded"];

    private static string FormatNumber(
        int? value,
        CultureInfo culture,
        IReadOnlyDictionary<string, string> translations) =>
        value?.ToString(culture) ?? translations["notRecorded"];

    private static string FormatBoolean(bool value, IReadOnlyDictionary<string, string> translations) =>
        translations[value ? "yes" : "no"];

    private static string FormatJson(string? data, IReadOnlyDictionary<string, string> translations)
    {
        if (string.IsNullOrWhiteSpace(data))
            return translations["noDetails"];

        try
        {
            using var parsed = JsonDocument.Parse(data);
            return JsonSerializer.Serialize(parsed.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (JsonException)
        {
            return translations["noDetails"];
        }
    }

    private static CultureInfo GetCulture(string locale) =>
        locale is "nl" or "fr" or "de" ? CultureInfo.GetCultureInfo(locale) : CultureInfo.GetCultureInfo("en");

    private sealed record PdfSection(
        PersonalDataExportRefTestData RefTest,
        IReadOnlyList<PersonalDataExportAuditEventData> AuditEvents,
        bool IncludeStoredFields,
        bool IsContinuation);
}

/// <summary>A fixed, non-sensitive error raised when an export cannot fit provider limits.</summary>
public sealed class PersonalDataExportSizeLimitException()
    : Exception("The personal-data export exceeds the supported PDF attachment limits.");
