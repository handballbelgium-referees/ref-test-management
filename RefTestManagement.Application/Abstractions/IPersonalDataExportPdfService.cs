using Handball.Belgium.RefTestManagement.Application.Models;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

/// <summary>Builds localized, in-memory PDF attachments for a verified data export.</summary>
public interface IPersonalDataExportPdfService
{
    Task<IReadOnlyList<EmailAttachment>> GenerateAttachmentsAsync(PersonalDataExportDocumentData document);
}
