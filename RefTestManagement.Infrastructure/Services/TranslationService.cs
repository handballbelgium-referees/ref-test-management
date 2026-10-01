namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

/// <summary>
/// Service for managing all application translations
/// </summary>
public interface ITranslationService
{
    IReadOnlyDictionary<string, string> GetEmailInvitationTranslations(string language);
    IReadOnlyDictionary<string, string> GetEmailResultsTranslations(string language);
    IReadOnlyDictionary<string, string> GetEmailReportTranslations(string language);
    IReadOnlyDictionary<string, string> GetEmailApprovalNotificationTranslations(string language);
    IReadOnlyDictionary<string, string> GetEmailApprovalDecisionTranslations(string language, bool isApproved);
    IReadOnlyDictionary<string, string> GetEmailPersonalDataExportVerificationTranslations(string language);
    IReadOnlyDictionary<string, string> GetEmailPersonalDataExportDeliveryTranslations(string language);
    IReadOnlyDictionary<string, string> GetPdfResultsTranslations(string language);
    IReadOnlyDictionary<string, string> GetPdfPersonalDataExportTranslations(string language);
    IReadOnlyDictionary<string, string> GetPdfReportTranslations(string language);
    IReadOnlyDictionary<string, string> GetReportColumnTranslations(string language);
    string GetLanguageDisplayName(string languageCode);
    string GetValidityText(string language, TimeSpan expiration);
}

public class TranslationService : ITranslationService
{
    // Cache translation dictionaries (allocated once)
    private readonly Dictionary<string, Dictionary<string, string>> _emailInvitationTranslations = InitializeEmailInvitationTranslations();
    private readonly Dictionary<string, Dictionary<string, string>> _emailResultsTranslations = InitializeEmailResultsTranslations();
    private readonly Dictionary<string, Dictionary<string, string>> _emailReportTranslations = InitializeEmailReportTranslations();
    private readonly Dictionary<string, Dictionary<string, string>> _emailApprovalNotificationTranslations = InitializeEmailApprovalNotificationTranslations();
    private readonly Dictionary<string, Dictionary<string, string>> _emailApprovalApprovedTranslations = InitializeEmailApprovalApprovedTranslations();
    private readonly Dictionary<string, Dictionary<string, string>> _emailApprovalRejectedTranslations = InitializeEmailApprovalRejectedTranslations();
    private readonly Dictionary<string, Dictionary<string, string>> _emailPersonalDataExportVerificationTranslations = InitializeEmailPersonalDataExportVerificationTranslations();
    private readonly Dictionary<string, Dictionary<string, string>> _emailPersonalDataExportDeliveryTranslations = InitializeEmailPersonalDataExportDeliveryTranslations();
    private readonly Dictionary<string, Dictionary<string, string>> _pdfResultsTranslations = InitializePdfResultsTranslations();
    private readonly Dictionary<string, Dictionary<string, string>> _pdfPersonalDataExportTranslations = InitializePdfPersonalDataExportTranslations();
    private readonly Dictionary<string, Dictionary<string, string>> _pdfReportTranslations = InitializePdfReportTranslations();
    private readonly Dictionary<string, Dictionary<string, string>> _reportColumnTranslations = InitializeReportColumnTranslations();
    private readonly Dictionary<string, string> _languageDisplayNames = InitializeLanguageDisplayNames();

    public IReadOnlyDictionary<string, string> GetEmailInvitationTranslations(string language)
    {
        return _emailInvitationTranslations.TryGetValue(language, out var translations)
            ? translations
            : _emailInvitationTranslations["en"];
    }

    public IReadOnlyDictionary<string, string> GetEmailResultsTranslations(string language)
    {
        return _emailResultsTranslations.TryGetValue(language, out var translations)
            ? translations
            : _emailResultsTranslations["en"];
    }

    public IReadOnlyDictionary<string, string> GetEmailReportTranslations(string language)
    {
        return _emailReportTranslations.TryGetValue(language, out var translations)
            ? translations
            : _emailReportTranslations["en"];
    }

    public IReadOnlyDictionary<string, string> GetEmailApprovalDecisionTranslations(string language, bool isApproved)
    {
        var dict = isApproved ? _emailApprovalApprovedTranslations : _emailApprovalRejectedTranslations;
        return dict.TryGetValue(language, out var translations) ? translations : dict["en"];
    }

    public IReadOnlyDictionary<string, string> GetEmailApprovalNotificationTranslations(string language)
    {
        return _emailApprovalNotificationTranslations.TryGetValue(language, out var translations)
            ? translations
            : _emailApprovalNotificationTranslations["en"];
    }

    public IReadOnlyDictionary<string, string> GetEmailPersonalDataExportVerificationTranslations(string language)
    {
        return _emailPersonalDataExportVerificationTranslations.TryGetValue(language, out var translations)
            ? translations
            : _emailPersonalDataExportVerificationTranslations["en"];
    }

    public IReadOnlyDictionary<string, string> GetEmailPersonalDataExportDeliveryTranslations(string language)
    {
        return _emailPersonalDataExportDeliveryTranslations.TryGetValue(language, out var translations)
            ? translations
            : _emailPersonalDataExportDeliveryTranslations["en"];
    }

    public IReadOnlyDictionary<string, string> GetPdfResultsTranslations(string language)
    {
        return _pdfResultsTranslations.TryGetValue(language, out var translations)
            ? translations
            : _pdfResultsTranslations["en"];
    }

    public IReadOnlyDictionary<string, string> GetPdfPersonalDataExportTranslations(string language)
    {
        return _pdfPersonalDataExportTranslations.TryGetValue(language, out var translations)
            ? translations
            : _pdfPersonalDataExportTranslations["en"];
    }

    public IReadOnlyDictionary<string, string> GetPdfReportTranslations(string language)
    {
        return _pdfReportTranslations.TryGetValue(language, out var translations)
            ? translations
            : _pdfReportTranslations["en"];
    }

    public IReadOnlyDictionary<string, string> GetReportColumnTranslations(string language)
    {
        return _reportColumnTranslations.TryGetValue(language, out var translations)
            ? translations
            : _reportColumnTranslations["en"];
    }

    public string GetLanguageDisplayName(string languageCode)
    {
        return _languageDisplayNames.TryGetValue(languageCode, out var displayName)
            ? displayName
            : languageCode.ToUpperInvariant();
    }

    public string GetValidityText(string language, TimeSpan expiration)
    {
        var totalHours = (int)expiration.TotalHours;
        var totalDays = (int)expiration.TotalDays;

        return language switch
        {
            "en" => totalHours < 24
                ? $"⏰ This RefTest is valid for {totalHours} {(totalHours == 1 ? "hour" : "hours")}"
                : $"⏰ This RefTest is valid for {totalDays} {(totalDays == 1 ? "day" : "days")}",
            "nl" => totalHours < 24
                ? $"⏰ Deze RefTest is {totalHours} {(totalHours == 1 ? "uur" : "uren")} geldig"
                : $"⏰ Deze RefTest is {totalDays} {(totalDays == 1 ? "dag" : "dagen")} geldig",
            "fr" => totalHours < 24
                ? $"⏰ Ce RefTest est valide pendant {totalHours} {(totalHours <= 1 ? "heure" : "heures")}"
                : $"⏰ Ce RefTest est valide pendant {totalDays} {(totalDays <= 1 ? "jour" : "jours")}",
            "de" => totalHours < 24
                ? $"⏰ Dieser RefTest ist {totalHours} {(totalHours == 1 ? "Stunde" : "Stunden")} lang gültig"
                : $"⏰ Dieser RefTest ist {totalDays} {(totalDays == 1 ? "Tag" : "Tage")} lang gültig",
            _ => $"⏰ Valid for {totalDays} days"
        };
    }

    private static Dictionary<string, string> InitializeLanguageDisplayNames()
    {
        return new Dictionary<string, string>
        {
            ["en"] = "English",
            ["nl"] = "Nederlands",
            ["fr"] = "Français",
            ["de"] = "Deutsch"
        };
    }

    private static Dictionary<string, Dictionary<string, string>> InitializeEmailInvitationTranslations()
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["displayName"] = "English",
                ["refTestDetails"] = "RefTest Details",
                ["questions"] = "Questions",
                ["timeLimit"] = "Time Limit",
                ["minutes"] = "minutes",
                ["greeting"] = "Hello",
                ["inviteText"] = "You have been invited to take the RefTest. Click the button below to start your RefTest:",
                ["startButton"] = "Start RefTest"
            },
            ["nl"] = new()
            {
                ["displayName"] = "Nederlands",
                ["refTestDetails"] = "RefTest Details",
                ["questions"] = "Vragen",
                ["timeLimit"] = "Tijdslimiet",
                ["minutes"] = "minuten",
                ["greeting"] = "Hallo",
                ["inviteText"] = "Je bent uitgenodigd om deel te nemen aan de RefTest. Klik op de knop hieronder om je RefTest te starten:",
                ["startButton"] = "RefTest Starten"
            },
            ["fr"] = new()
            {
                ["displayName"] = "Français",
                ["refTestDetails"] = "Détails du RefTest",
                ["questions"] = "Questions",
                ["timeLimit"] = "Limite de Temps",
                ["minutes"] = "minutes",
                ["greeting"] = "Bonjour",
                ["inviteText"] = "Vous êtes invité à participer au RefTest. Cliquez sur le bouton ci-dessous pour commencer votre RefTest:",
                ["startButton"] = "Démarrer le RefTest"
            },
            ["de"] = new()
            {
                ["displayName"] = "Deutsch",
                ["refTestDetails"] = "RefTest-Details",
                ["questions"] = "Fragen",
                ["timeLimit"] = "Zeitlimit",
                ["minutes"] = "Minuten",
                ["greeting"] = "Hallo",
                ["inviteText"] = "Sie wurden eingeladen, am RefTest teilzunehmen. Klicken Sie auf die Schaltfläche unten, um Ihren RefTest zu starten:",
                ["startButton"] = "RefTest starten"
            }
        };
    }

    private static Dictionary<string, Dictionary<string, string>> InitializeEmailResultsTranslations()
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["displayName"] = "English",
                ["passed"] = "PASSED",
                ["notPassed"] = "NOT PASSED",
                ["score"] = "Your Score",
                ["questions"] = "Q",
                ["answers"] = "A",
                ["percentage"] = "Percentage",
                ["passedMessage"] = "🎉 Congratulations! You passed the RefTest!",
                ["failedMessage"] = "📚 Keep studying and good luck next time!",
                ["greeting"] = "Dear",
                ["passedText"] = "Congratulations! You have successfully passed the RefTest! Your knowledge of handball regulations is excellent.",
                ["failedText"] = "Thank you for taking the RefTest. A score of 80% or higher is required to pass. Please review the rules and try again.",
                ["pdfNote"] = "📎 <strong>Detailed results are available in the attached PDF documents</strong>"
            },
            ["nl"] = new()
            {
                ["displayName"] = "Nederlands",
                ["passed"] = "GESLAAGD",
                ["notPassed"] = "NIET GESLAAGD",
                ["score"] = "Jouw Score",
                ["questions"] = "V",
                ["answers"] = "A",
                ["percentage"] = "Percentage",
                ["passedMessage"] = "🎉 Gefeliciteerd! Je bent geslaagd!",
                ["failedMessage"] = "📚 Blijf studeren en veel succes volgende keer!",
                ["greeting"] = "Beste",
                ["passedText"] = "Gefeliciteerd! Je bent geslaagd voor de RefTest! Je kennis van handbalregels is uitstekend.",
                ["failedText"] = "Bedankt voor het deelnemen aan de RefTest. Een score van 80% of hoger is vereist om te slagen. Bekijk de regels en probeer het opnieuw.",
                ["pdfNote"] = "📎 <strong>Gedetailleerde resultaten zijn beschikbaar in de bijgevoegde PDF-documenten</strong>"
            },
            ["fr"] = new()
            {
                ["displayName"] = "Français",
                ["passed"] = "RÉUSSI",
                ["notPassed"] = "NON RÉUSSI",
                ["score"] = "Votre Score",
                ["questions"] = "Q",
                ["answers"] = "R",
                ["percentage"] = "Pourcentage",
                ["passedMessage"] = "🎉 Félicitations ! Vous avez réussi !",
                ["failedMessage"] = "📚 Continuez à étudier et bonne chance la prochaine fois !",
                ["greeting"] = "Bonjour",
                ["passedText"] = "Félicitations! Vous avez réussi le RefTest ! Votre connaissance des règles de handball est excellente.",
                ["failedText"] = "Merci d'avoir participé au RefTest. Un score de 80% ou plus est requis pour réussir. Veuillez réviser les règles et réessayer.",
                ["pdfNote"] = "📎 <strong>Les résultats détaillés sont disponibles dans les documents PDF joints</strong>"
            },
            ["de"] = new()
            {
                ["displayName"] = "Deutsch",
                ["passed"] = "BESTANDEN",
                ["notPassed"] = "NICHT BESTANDEN",
                ["score"] = "Ihre Punktzahl",
                ["questions"] = "F",
                ["answers"] = "A",
                ["percentage"] = "Prozentsatz",
                ["passedMessage"] = "🎉 Herzlichen Glückwunsch! Sie haben bestanden!",
                ["failedMessage"] = "📚 Lernen Sie weiter und viel Glück beim nächsten Mal!",
                ["greeting"] = "Hallo",
                ["passedText"] = "Herzlichen Glückwunsch! Sie haben das RefTest bestanden! Ihre Kenntnisse der Handballregeln sind ausgezeichnet.",
                ["failedText"] = "Vielen Dank, dass Sie am RefTest teilgenommen haben. Eine Punktzahl von 80% oder höher ist erforderlich zum Bestehen. Bitte überprüfen Sie die Regeln und versuchen Sie es erneut.",
                ["pdfNote"] = "📎 <strong>Detaillierte Ergebnisse sind in den beigefügten PDF-Dokumenten verfügbar</strong>"
            }
        };
    }

    private static Dictionary<string, Dictionary<string, string>> InitializeEmailReportTranslations()
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["displayName"] = "English",
                ["introText"] = "This is an automatically generated report containing RefTest data.",
                ["reportDate"] = "Report Date",
                ["numberOfRefTests"] = "Number of RefTests",
                ["attachmentText"] = "The report is attached in both Excel (.xlsx) and PDF formats.",
                ["reportDetails"] = "Report Details"
            },
            ["nl"] = new()
            {
                ["displayName"] = "Nederlands",
                ["introText"] = "Dit is een automatisch gegenereerd rapport met RefTest-gegevens.",
                ["reportDate"] = "Rapportdatum",
                ["numberOfRefTests"] = "Aantal RefTests",
                ["attachmentText"] = "Het rapport is bijgevoegd in zowel Excel- (.xlsx) als PDF-formaat.",
                ["reportDetails"] = "Rapportdetails"
            },
            ["fr"] = new()
            {
                ["displayName"] = "Français",
                ["introText"] = "Ceci est un rapport généré automatiquement contenant des données RefTest.",
                ["reportDate"] = "Date du Rapport",
                ["numberOfRefTests"] = "Nombre de RefTests",
                ["attachmentText"] = "Le rapport est joint aux formats Excel (.xlsx) et PDF.",
                ["reportDetails"] = "Détails du Rapport"
            },
            ["de"] = new()
            {
                ["displayName"] = "Deutsch",
                ["introText"] = "Dies ist ein automatisch generierter Bericht mit RefTest-Daten.",
                ["reportDate"] = "Berichtsdatum",
                ["numberOfRefTests"] = "Anzahl der RefTests",
                ["attachmentText"] = "Der Bericht ist sowohl im Excel- (.xlsx) als auch im PDF-Format beigefügt.",
                ["reportDetails"] = "Berichtsdetails"
            }
        };
    }

    private static Dictionary<string, Dictionary<string, string>> InitializeEmailApprovalNotificationTranslations()
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["subject"] = "RefTests Awaiting Your Approval",
                ["heading"] = "RefTests Awaiting Approval",
                ["introText"] = "The following RefTests were created and require your approval before invitations are sent.",
                ["createdBy"] = "Created by",
                ["title"] = "Title",
                ["tableNameHeader"] = "Name",
                ["tableEmailHeader"] = "Email",
                ["tableScheduledAtHeader"] = "Scheduled At",
                ["reviewButton"] = "Review Pending Tests",
                ["footerNote"] = "You are receiving this email because you have approval rights in RefTest Management."
            },
            ["nl"] = new()
            {
                ["subject"] = "RefTests wachten op uw goedkeuring",
                ["heading"] = "RefTests wachten op goedkeuring",
                ["introText"] = "De volgende RefTests zijn aangemaakt en wachten op uw goedkeuring voordat uitnodigingen worden verstuurd.",
                ["createdBy"] = "Aangemaakt door",
                ["title"] = "Titel",
                ["tableNameHeader"] = "Naam",
                ["tableEmailHeader"] = "E-mail",
                ["tableScheduledAtHeader"] = "Gepland Op",
                ["reviewButton"] = "Openstaande tests beoordelen",
                ["footerNote"] = "U ontvangt deze e-mail omdat u goedkeuringsrechten heeft in RefTest Management."
            },
            ["fr"] = new()
            {
                ["subject"] = "RefTests en attente de votre approbation",
                ["heading"] = "RefTests en attente d'approbation",
                ["introText"] = "Les RefTests suivants ont été créés et nécessitent votre approbation avant l'envoi des invitations.",
                ["createdBy"] = "Créé par",
                ["title"] = "Titre",
                ["tableNameHeader"] = "Nom",
                ["tableEmailHeader"] = "E-mail",
                ["tableScheduledAtHeader"] = "Planifié Le",
                ["reviewButton"] = "Examiner les tests en attente",
                ["footerNote"] = "Vous recevez cet e-mail car vous disposez de droits d'approbation dans RefTest Management."
            },
            ["de"] = new()
            {
                ["subject"] = "RefTests warten auf Ihre Genehmigung",
                ["heading"] = "RefTests warten auf Genehmigung",
                ["introText"] = "Die folgenden RefTests wurden erstellt und warten auf Ihre Genehmigung, bevor Einladungen verschickt werden.",
                ["createdBy"] = "Erstellt von",
                ["title"] = "Titel",
                ["tableNameHeader"] = "Name",
                ["tableEmailHeader"] = "E-Mail",
                ["tableScheduledAtHeader"] = "Geplant Am",
                ["reviewButton"] = "Ausstehende Tests prüfen",
                ["footerNote"] = "Sie erhalten diese E-Mail, weil Sie Genehmigungsrechte in RefTest Management haben."
            }
        };
    }

    private static Dictionary<string, Dictionary<string, string>> InitializeEmailApprovalApprovedTranslations()
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["subject"] = "Your RefTests Have Been Approved",
                ["heading"] = "RefTests Approved ✅",
                ["introText"] = "Great news! The following RefTests you created have been approved and are now ready.",
                ["approvedBy"] = "Approved by",
                ["tableNameHeader"] = "Name",
                ["tableEmailHeader"] = "Email",
                ["footerNote"] = "You are receiving this email because you created these RefTests."
            },
            ["nl"] = new()
            {
                ["subject"] = "Uw RefTests zijn goedgekeurd",
                ["heading"] = "RefTests goedgekeurd ✅",
                ["introText"] = "Goed nieuws! De volgende RefTests die u heeft aangemaakt zijn goedgekeurd en klaar voor gebruik.",
                ["approvedBy"] = "Goedgekeurd door",
                ["tableNameHeader"] = "Naam",
                ["tableEmailHeader"] = "E-mail",
                ["footerNote"] = "U ontvangt deze e-mail omdat u deze RefTests heeft aangemaakt."
            },
            ["fr"] = new()
            {
                ["subject"] = "Vos RefTests ont été approuvés",
                ["heading"] = "RefTests approuvés ✅",
                ["introText"] = "Bonne nouvelle ! Les RefTests suivants que vous avez créés ont été approuvés et sont maintenant prêts.",
                ["approvedBy"] = "Approuvé par",
                ["tableNameHeader"] = "Nom",
                ["tableEmailHeader"] = "E-mail",
                ["footerNote"] = "Vous recevez cet e-mail car vous avez créé ces RefTests."
            },
            ["de"] = new()
            {
                ["subject"] = "Ihre RefTests wurden genehmigt",
                ["heading"] = "RefTests genehmigt ✅",
                ["introText"] = "Gute Neuigkeiten! Die folgenden RefTests, die Sie erstellt haben, wurden genehmigt und sind jetzt bereit.",
                ["approvedBy"] = "Genehmigt von",
                ["tableNameHeader"] = "Name",
                ["tableEmailHeader"] = "E-Mail",
                ["footerNote"] = "Sie erhalten diese E-Mail, weil Sie diese RefTests erstellt haben."
            }
        };
    }

    private static Dictionary<string, Dictionary<string, string>> InitializeEmailApprovalRejectedTranslations()
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["subject"] = "Your RefTests Have Been Rejected",
                ["heading"] = "RefTests Rejected ❌",
                ["introText"] = "Unfortunately, the following RefTests you created have been rejected.",
                ["rejectedBy"] = "Rejected by",
                ["reason"] = "Reason",
                ["tableNameHeader"] = "Name",
                ["tableEmailHeader"] = "Email",
                ["footerNote"] = "You are receiving this email because you created these RefTests."
            },
            ["nl"] = new()
            {
                ["subject"] = "Uw RefTests zijn afgewezen",
                ["heading"] = "RefTests afgewezen ❌",
                ["introText"] = "Helaas zijn de volgende RefTests die u heeft aangemaakt afgewezen.",
                ["rejectedBy"] = "Afgewezen door",
                ["reason"] = "Reden",
                ["tableNameHeader"] = "Naam",
                ["tableEmailHeader"] = "E-mail",
                ["footerNote"] = "U ontvangt deze e-mail omdat u deze RefTests heeft aangemaakt."
            },
            ["fr"] = new()
            {
                ["subject"] = "Vos RefTests ont été refusés",
                ["heading"] = "RefTests refusés ❌",
                ["introText"] = "Malheureusement, les RefTests suivants que vous avez créés ont été refusés.",
                ["rejectedBy"] = "Refusé par",
                ["reason"] = "Raison",
                ["tableNameHeader"] = "Nom",
                ["tableEmailHeader"] = "E-mail",
                ["footerNote"] = "Vous recevez cet e-mail car vous avez créé ces RefTests."
            },
            ["de"] = new()
            {
                ["subject"] = "Ihre RefTests wurden abgelehnt",
                ["heading"] = "RefTests abgelehnt ❌",
                ["introText"] = "Leider wurden die folgenden RefTests, die Sie erstellt haben, abgelehnt.",
                ["rejectedBy"] = "Abgelehnt von",
                ["reason"] = "Grund",
                ["tableNameHeader"] = "Name",
                ["tableEmailHeader"] = "E-Mail",
                ["footerNote"] = "Sie erhalten diese E-Mail, weil Sie diese RefTests erstellt haben."
            }
        };
    }

    private static Dictionary<string, Dictionary<string, string>> InitializeEmailPersonalDataExportVerificationTranslations()
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["subject"] = "Confirm your personal data export request",
                ["heading"] = "Confirm your email address",
                ["introText"] = "Someone asked to receive a copy of saved RefTest data associated with this email address. Confirm that you made this request.",
                ["confirmButton"] = "Confirm request",
                ["expiryNote"] = "This confirmation link expires in {0} hours.",
                ["ignoreNote"] = "If you did not request this, you can ignore this email. No export will be prepared."
            },
            ["nl"] = new()
            {
                ["subject"] = "Bevestig uw aanvraag voor een kopie van uw persoonsgegevens",
                ["heading"] = "Bevestig uw e-mailadres",
                ["introText"] = "Iemand heeft gevraagd om een kopie van opgeslagen RefTest-gegevens die aan dit e-mailadres zijn gekoppeld. Bevestig dat u dit hebt aangevraagd.",
                ["confirmButton"] = "Aanvraag bevestigen",
                ["expiryNote"] = "Deze bevestigingslink verloopt over {0} uur.",
                ["ignoreNote"] = "Hebt u dit niet aangevraagd? U kunt deze e-mail negeren. Er wordt geen export voorbereid."
            },
            ["fr"] = new()
            {
                ["subject"] = "Confirmez votre demande d’exportation de données personnelles",
                ["heading"] = "Confirmez votre adresse e-mail",
                ["introText"] = "Une personne a demandé une copie des données RefTest enregistrées associées à cette adresse e-mail. Confirmez que vous êtes à l’origine de cette demande.",
                ["confirmButton"] = "Confirmer la demande",
                ["expiryNote"] = "Ce lien de confirmation expire dans {0} heures.",
                ["ignoreNote"] = "Si vous n’avez pas fait cette demande, vous pouvez ignorer cet e-mail. Aucune exportation ne sera préparée."
            },
            ["de"] = new()
            {
                ["subject"] = "Bestätigen Sie Ihre Anfrage zum Export personenbezogener Daten",
                ["heading"] = "Bestätigen Sie Ihre E-Mail-Adresse",
                ["introText"] = "Jemand hat eine Kopie der gespeicherten RefTest-Daten angefordert, die mit dieser E-Mail-Adresse verknüpft sind. Bestätigen Sie, dass Sie diese Anfrage gestellt haben.",
                ["confirmButton"] = "Anfrage bestätigen",
                ["expiryNote"] = "Dieser Bestätigungslink läuft in {0} Stunden ab.",
                ["ignoreNote"] = "Wenn Sie diese Anfrage nicht gestellt haben, können Sie diese E-Mail ignorieren. Es wird kein Export vorbereitet."
            }
        };
    }

    private static Dictionary<string, Dictionary<string, string>> InitializeEmailPersonalDataExportDeliveryTranslations()
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["subject"] = "Your personal data export",
                ["heading"] = "Your RefTest personal data",
                ["body"] = "Attached is the PDF copy of the saved RefTest data associated with your verified email address."
            },
            ["nl"] = new()
            {
                ["subject"] = "Uw export van persoonsgegevens",
                ["heading"] = "Uw RefTest-persoonsgegevens",
                ["body"] = "In de bijlage vindt u de PDF met de opgeslagen RefTest-gegevens die bij uw geverifieerde e-mailadres horen."
            },
            ["fr"] = new()
            {
                ["subject"] = "Votre exportation de données personnelles",
                ["heading"] = "Vos données personnelles RefTest",
                ["body"] = "Vous trouverez en pièce jointe le PDF des données RefTest enregistrées associées à votre adresse e-mail vérifiée."
            },
            ["de"] = new()
            {
                ["subject"] = "Ihr Export personenbezogener Daten",
                ["heading"] = "Ihre RefTest-Daten",
                ["body"] = "Im Anhang finden Sie die PDF-Datei mit den gespeicherten RefTest-Daten, die Ihrer bestätigten E-Mail-Adresse zugeordnet sind."
            }
        };
    }

    private static Dictionary<string, Dictionary<string, string>> InitializePdfResultsTranslations()    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["resultsTitle"] = "Your Results",
                ["name"] = "Name",
                ["percentage"] = "Percentage",
                ["score"] = "Your score",
                ["questions"] = "Q",
                ["answers"] = "A",
                ["reviewAnswers"] = "Review Answers",
                ["question"] = "Question",
                ["yourAnswer"] = "Your answer",
                ["correctAnswer"] = "Correct answer"
            },
            ["nl"] = new()
            {
                ["resultsTitle"] = "Jouw Resultaten",
                ["name"] = "Naam",
                ["percentage"] = "Percentage",
                ["score"] = "Jouw score",
                ["questions"] = "V",
                ["answers"] = "A",
                ["reviewAnswers"] = "Antwoorden Beoordelen",
                ["question"] = "Vraag",
                ["yourAnswer"] = "Jouw antwoord",
                ["correctAnswer"] = "Juist antwoord"
            },
            ["fr"] = new()
            {
                ["resultsTitle"] = "Vos Résultats",
                ["name"] = "Nom",
                ["percentage"] = "Pourcentage",
                ["score"] = "Votre score",
                ["questions"] = "Q",
                ["answers"] = "R",
                ["reviewAnswers"] = "Réviser les Réponses",
                ["question"] = "Question",
                ["yourAnswer"] = "Votre réponse",
                ["correctAnswer"] = "Réponse correcte"
            },
            ["de"] = new()
            {
                ["resultsTitle"] = "Ihre Ergebnisse",
                ["name"] = "Name",
                ["percentage"] = "Prozent",
                ["score"] = "Ihre Punktzahl",
                ["questions"] = "F",
                ["answers"] = "A",
                ["reviewAnswers"] = "Antworten Überprüfen",
                ["question"] = "Frage",
                ["yourAnswer"] = "Ihre Antwort",
                ["correctAnswer"] = "Richtige Antwort"
            }
        };
    }

    private static Dictionary<string, Dictionary<string, string>> InitializePdfPersonalDataExportTranslations()
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["title"] = "Personal data export",
                ["recipientEmail"] = "Verified email",
                ["refTest"] = "RefTest",
                ["refTestId"] = "RefTest ID",
                ["titleId"] = "Title ID",
                ["firstName"] = "First name",
                ["lastName"] = "Last name",
                ["email"] = "Email",
                ["status"] = "Status",
                ["status.Pending"] = "Pending",
                ["status.InProgress"] = "In progress",
                ["status.Completed"] = "Completed",
                ["status.Expired"] = "Expired",
                ["status.PendingApproval"] = "Pending approval",
                ["status.Rejected"] = "Rejected",
                ["sendInvitationsAutomatically"] = "Automatic invitations",
                ["invitationSentAt"] = "Invitation sent",
                ["numberOfQuestions"] = "Number of questions",
                ["maxTimeInMinutes"] = "Time limit (minutes)",
                ["questionIds"] = "Question IDs",
                ["createdAt"] = "Created",
                ["startedAt"] = "Started",
                ["completedAt"] = "Completed",
                ["expiredAt"] = "Expired",
                ["currentQuestionIndex"] = "Current question index",
                ["questionScore"] = "Question score",
                ["answerScore"] = "Answer score",
                ["answerTotal"] = "Total answers",
                ["percentage"] = "Percentage",
                ["selectedAnswerIds"] = "Selected answer IDs",
                ["wrongQuestionIds"] = "Incorrect question IDs",
                ["wrongAnswerIds"] = "Incorrect answer IDs",
                ["sendResultsAutomatically"] = "Automatic results",
                ["resultsSentAt"] = "Results sent",
                ["language"] = "Language",
                ["privacyNoticeVersion"] = "Privacy notice version",
                ["privacyNoticeAcceptedAt"] = "Privacy notice accepted",
                ["scheduledAt"] = "Scheduled",
                ["events"] = "Retained audit events",
                ["eventType"] = "Event",
                ["event.RefTestCreated"] = "Created",
                ["event.RefTestDetailsUpdated"] = "Participant details updated",
                ["event.RefTestConfigurationUpdated"] = "Configuration updated",
                ["event.RefTestNotificationSettingsUpdated"] = "Notification settings updated",
                ["event.RefTestTimeExtended"] = "Time extended",
                ["event.RefTestSoftReset"] = "RefTest reset",
                ["event.RefTestStarted"] = "Started",
                ["event.RefTestPrivacyNoticeAccepted"] = "Privacy notice accepted",
                ["event.RefTestCompleted"] = "Completed",
                ["event.RefTestApproved"] = "Approved",
                ["event.RefTestRejected"] = "Rejected",
                ["event.RefTestExpired"] = "Expired",
                ["event.RefTestDeleted"] = "Deleted",
                ["event.RefTestAnonymized"] = "Personal data anonymized",
                ["event.RefTestRevived"] = "Revived",
                ["event.RefTestInvitationSent"] = "Invitation sent",
                ["event.RefTestResultsSent"] = "Results sent",
                ["event.RefTestTokenRegenerated"] = "Invitation token regenerated",
                ["event.RefTestHardReset"] = "RefTest fully reset",
                ["timestamp"] = "Date and time",
                ["actor"] = "Actor",
                ["actorEmail"] = "Actor email",
                ["actorParticipant"] = "Participant",
                ["actorVerifiedParticipant"] = "Verified participant",
                ["actorSystem"] = "System",
                ["actorStaff"] = "Staff member",
                ["actorOther"] = "Other",
                ["details"] = "Details",
                ["archived"] = "Archived",
                ["redactedAt"] = "Details redacted",
                ["yes"] = "Yes",
                ["no"] = "No",
                ["notRecorded"] = "Not recorded",
                ["noDetails"] = "No event details retained",
                ["part"] = "Part {0} of {1}",
                ["continued"] = "continued",
                ["version"] = "Version",
                ["page"] = "Page",
                ["of"] = "of",
                ["footer"] = "RefTest Management — Personal data export"
            },
            ["nl"] = new()
            {
                ["title"] = "Export van persoonsgegevens",
                ["recipientEmail"] = "Geverifieerd e-mailadres",
                ["refTest"] = "RefTest",
                ["refTestId"] = "RefTest-ID",
                ["titleId"] = "Titel-ID",
                ["firstName"] = "Voornaam",
                ["lastName"] = "Achternaam",
                ["email"] = "E-mailadres",
                ["status"] = "Status",
                ["status.Pending"] = "In afwachting",
                ["status.InProgress"] = "Bezig",
                ["status.Completed"] = "Voltooid",
                ["status.Expired"] = "Verlopen",
                ["status.PendingApproval"] = "Wacht op goedkeuring",
                ["status.Rejected"] = "Afgewezen",
                ["sendInvitationsAutomatically"] = "Uitnodigingen automatisch verzenden",
                ["invitationSentAt"] = "Uitnodiging verzonden",
                ["numberOfQuestions"] = "Aantal vragen",
                ["maxTimeInMinutes"] = "Tijdslimiet (minuten)",
                ["questionIds"] = "Vraag-ID's",
                ["createdAt"] = "Aangemaakt",
                ["startedAt"] = "Gestart",
                ["completedAt"] = "Voltooid",
                ["expiredAt"] = "Verlopen",
                ["currentQuestionIndex"] = "Huidige vraagindex",
                ["questionScore"] = "Vragenscore",
                ["answerScore"] = "Antwoordscore",
                ["answerTotal"] = "Totaal aantal antwoorden",
                ["percentage"] = "Percentage",
                ["selectedAnswerIds"] = "Geselecteerde antwoord-ID's",
                ["wrongQuestionIds"] = "Onjuiste vraag-ID's",
                ["wrongAnswerIds"] = "Onjuiste antwoord-ID's",
                ["sendResultsAutomatically"] = "Resultaten automatisch verzenden",
                ["resultsSentAt"] = "Resultaten verzonden",
                ["language"] = "Taal",
                ["privacyNoticeVersion"] = "Versie privacyverklaring",
                ["privacyNoticeAcceptedAt"] = "Privacyverklaring aanvaard",
                ["scheduledAt"] = "Gepland",
                ["events"] = "Bewaarde auditgebeurtenissen",
                ["eventType"] = "Gebeurtenis",
                ["event.RefTestCreated"] = "Aangemaakt",
                ["event.RefTestDetailsUpdated"] = "Deelnemergegevens bijgewerkt",
                ["event.RefTestConfigurationUpdated"] = "Configuratie bijgewerkt",
                ["event.RefTestNotificationSettingsUpdated"] = "Meldingsinstellingen bijgewerkt",
                ["event.RefTestTimeExtended"] = "Tijd verlengd",
                ["event.RefTestSoftReset"] = "RefTest gereset",
                ["event.RefTestStarted"] = "Gestart",
                ["event.RefTestPrivacyNoticeAccepted"] = "Privacyverklaring aanvaard",
                ["event.RefTestCompleted"] = "Voltooid",
                ["event.RefTestApproved"] = "Goedgekeurd",
                ["event.RefTestRejected"] = "Afgewezen",
                ["event.RefTestExpired"] = "Verlopen",
                ["event.RefTestDeleted"] = "Verwijderd",
                ["event.RefTestAnonymized"] = "Persoonsgegevens geanonimiseerd",
                ["event.RefTestRevived"] = "Opnieuw geactiveerd",
                ["event.RefTestInvitationSent"] = "Uitnodiging verzonden",
                ["event.RefTestResultsSent"] = "Resultaten verzonden",
                ["event.RefTestTokenRegenerated"] = "Uitnodigingstoken vernieuwd",
                ["event.RefTestHardReset"] = "RefTest volledig gereset",
                ["timestamp"] = "Datum en tijd",
                ["actor"] = "Actor",
                ["actorEmail"] = "E-mailadres actor",
                ["actorParticipant"] = "Deelnemer",
                ["actorVerifiedParticipant"] = "Geverifieerde deelnemer",
                ["actorSystem"] = "Systeem",
                ["actorStaff"] = "Medewerker",
                ["actorOther"] = "Overige",
                ["details"] = "Details",
                ["archived"] = "Gearchiveerd",
                ["redactedAt"] = "Details gewist",
                ["yes"] = "Ja",
                ["no"] = "Nee",
                ["notRecorded"] = "Niet geregistreerd",
                ["noDetails"] = "Geen gebeurtenisdetails bewaard",
                ["part"] = "Deel {0} van {1}",
                ["continued"] = "vervolg",
                ["version"] = "Versie",
                ["page"] = "Pagina",
                ["of"] = "van",
                ["footer"] = "RefTest Management — Export van persoonsgegevens"
            },
            ["fr"] = new()
            {
                ["title"] = "Exportation de données personnelles",
                ["recipientEmail"] = "Adresse e-mail vérifiée",
                ["refTest"] = "RefTest",
                ["refTestId"] = "ID RefTest",
                ["titleId"] = "ID du titre",
                ["firstName"] = "Prénom",
                ["lastName"] = "Nom",
                ["email"] = "Adresse e-mail",
                ["status"] = "Statut",
                ["status.Pending"] = "En attente",
                ["status.InProgress"] = "En cours",
                ["status.Completed"] = "Terminé",
                ["status.Expired"] = "Expiré",
                ["status.PendingApproval"] = "En attente d’approbation",
                ["status.Rejected"] = "Refusé",
                ["sendInvitationsAutomatically"] = "Envoi automatique des invitations",
                ["invitationSentAt"] = "Invitation envoyée",
                ["numberOfQuestions"] = "Nombre de questions",
                ["maxTimeInMinutes"] = "Limite de temps (minutes)",
                ["questionIds"] = "Identifiants des questions",
                ["createdAt"] = "Créé",
                ["startedAt"] = "Commencé",
                ["completedAt"] = "Terminé",
                ["expiredAt"] = "Expiré",
                ["currentQuestionIndex"] = "Index de la question actuelle",
                ["questionScore"] = "Score des questions",
                ["answerScore"] = "Score des réponses",
                ["answerTotal"] = "Nombre total de réponses",
                ["percentage"] = "Pourcentage",
                ["selectedAnswerIds"] = "Identifiants des réponses sélectionnées",
                ["wrongQuestionIds"] = "Identifiants des questions incorrectes",
                ["wrongAnswerIds"] = "Identifiants des réponses incorrectes",
                ["sendResultsAutomatically"] = "Envoi automatique des résultats",
                ["resultsSentAt"] = "Résultats envoyés",
                ["language"] = "Langue",
                ["privacyNoticeVersion"] = "Version de l’avis de confidentialité",
                ["privacyNoticeAcceptedAt"] = "Avis de confidentialité accepté",
                ["scheduledAt"] = "Planifié",
                ["events"] = "Événements d’audit conservés",
                ["eventType"] = "Événement",
                ["event.RefTestCreated"] = "Créé",
                ["event.RefTestDetailsUpdated"] = "Données du participant modifiées",
                ["event.RefTestConfigurationUpdated"] = "Configuration modifiée",
                ["event.RefTestNotificationSettingsUpdated"] = "Paramètres de notification modifiés",
                ["event.RefTestTimeExtended"] = "Durée prolongée",
                ["event.RefTestSoftReset"] = "RefTest réinitialisé",
                ["event.RefTestStarted"] = "Commencé",
                ["event.RefTestPrivacyNoticeAccepted"] = "Avis de confidentialité accepté",
                ["event.RefTestCompleted"] = "Terminé",
                ["event.RefTestApproved"] = "Approuvé",
                ["event.RefTestRejected"] = "Refusé",
                ["event.RefTestExpired"] = "Expiré",
                ["event.RefTestDeleted"] = "Supprimé",
                ["event.RefTestAnonymized"] = "Données personnelles anonymisées",
                ["event.RefTestRevived"] = "Réactivé",
                ["event.RefTestInvitationSent"] = "Invitation envoyée",
                ["event.RefTestResultsSent"] = "Résultats envoyés",
                ["event.RefTestTokenRegenerated"] = "Jeton d’invitation renouvelé",
                ["event.RefTestHardReset"] = "RefTest entièrement réinitialisé",
                ["timestamp"] = "Date et heure",
                ["actor"] = "Auteur",
                ["actorEmail"] = "E-mail de l’auteur",
                ["actorParticipant"] = "Participant",
                ["actorVerifiedParticipant"] = "Participant vérifié",
                ["actorSystem"] = "Système",
                ["actorStaff"] = "Membre du personnel",
                ["actorOther"] = "Autre",
                ["details"] = "Détails",
                ["archived"] = "Archivé",
                ["redactedAt"] = "Détails expurgés",
                ["yes"] = "Oui",
                ["no"] = "Non",
                ["notRecorded"] = "Non enregistré",
                ["noDetails"] = "Aucun détail d’événement conservé",
                ["part"] = "Partie {0} sur {1}",
                ["continued"] = "suite",
                ["version"] = "Version",
                ["page"] = "Page",
                ["of"] = "sur",
                ["footer"] = "RefTest Management — Exportation de données personnelles"
            },
            ["de"] = new()
            {
                ["title"] = "Export personenbezogener Daten",
                ["recipientEmail"] = "Bestätigte E-Mail-Adresse",
                ["refTest"] = "RefTest",
                ["refTestId"] = "RefTest-ID",
                ["titleId"] = "Titel-ID",
                ["firstName"] = "Vorname",
                ["lastName"] = "Nachname",
                ["email"] = "E-Mail-Adresse",
                ["status"] = "Status",
                ["status.Pending"] = "Ausstehend",
                ["status.InProgress"] = "In Bearbeitung",
                ["status.Completed"] = "Abgeschlossen",
                ["status.Expired"] = "Abgelaufen",
                ["status.PendingApproval"] = "Ausstehende Genehmigung",
                ["status.Rejected"] = "Abgelehnt",
                ["sendInvitationsAutomatically"] = "Einladungen automatisch senden",
                ["invitationSentAt"] = "Einladung gesendet",
                ["numberOfQuestions"] = "Anzahl der Fragen",
                ["maxTimeInMinutes"] = "Zeitlimit (Minuten)",
                ["questionIds"] = "Fragen-IDs",
                ["createdAt"] = "Erstellt",
                ["startedAt"] = "Gestartet",
                ["completedAt"] = "Abgeschlossen",
                ["expiredAt"] = "Abgelaufen",
                ["currentQuestionIndex"] = "Aktueller Fragenindex",
                ["questionScore"] = "Fragenpunktzahl",
                ["answerScore"] = "Antwortpunktzahl",
                ["answerTotal"] = "Antworten insgesamt",
                ["percentage"] = "Prozentsatz",
                ["selectedAnswerIds"] = "IDs ausgewählter Antworten",
                ["wrongQuestionIds"] = "IDs falscher Fragen",
                ["wrongAnswerIds"] = "IDs falscher Antworten",
                ["sendResultsAutomatically"] = "Ergebnisse automatisch senden",
                ["resultsSentAt"] = "Ergebnisse gesendet",
                ["language"] = "Sprache",
                ["privacyNoticeVersion"] = "Version des Datenschutzhinweises",
                ["privacyNoticeAcceptedAt"] = "Datenschutzhinweis akzeptiert",
                ["scheduledAt"] = "Geplant",
                ["events"] = "Aufbewahrte Audit-Ereignisse",
                ["eventType"] = "Ereignis",
                ["event.RefTestCreated"] = "Erstellt",
                ["event.RefTestDetailsUpdated"] = "Teilnehmerdaten aktualisiert",
                ["event.RefTestConfigurationUpdated"] = "Konfiguration aktualisiert",
                ["event.RefTestNotificationSettingsUpdated"] = "Benachrichtigungseinstellungen aktualisiert",
                ["event.RefTestTimeExtended"] = "Zeit verlängert",
                ["event.RefTestSoftReset"] = "RefTest zurückgesetzt",
                ["event.RefTestStarted"] = "Gestartet",
                ["event.RefTestPrivacyNoticeAccepted"] = "Datenschutzhinweis akzeptiert",
                ["event.RefTestCompleted"] = "Abgeschlossen",
                ["event.RefTestApproved"] = "Genehmigt",
                ["event.RefTestRejected"] = "Abgelehnt",
                ["event.RefTestExpired"] = "Abgelaufen",
                ["event.RefTestDeleted"] = "Gelöscht",
                ["event.RefTestAnonymized"] = "Personenbezogene Daten anonymisiert",
                ["event.RefTestRevived"] = "Reaktiviert",
                ["event.RefTestInvitationSent"] = "Einladung gesendet",
                ["event.RefTestResultsSent"] = "Ergebnisse gesendet",
                ["event.RefTestTokenRegenerated"] = "Einladungstoken erneuert",
                ["event.RefTestHardReset"] = "RefTest vollständig zurückgesetzt",
                ["timestamp"] = "Datum und Uhrzeit",
                ["actor"] = "Akteur",
                ["actorEmail"] = "E-Mail des Akteurs",
                ["actorParticipant"] = "Teilnehmer",
                ["actorVerifiedParticipant"] = "Verifizierter Teilnehmer",
                ["actorSystem"] = "System",
                ["actorStaff"] = "Mitarbeiter",
                ["actorOther"] = "Sonstige",
                ["details"] = "Details",
                ["archived"] = "Archiviert",
                ["redactedAt"] = "Details geschwärzt",
                ["yes"] = "Ja",
                ["no"] = "Nein",
                ["notRecorded"] = "Nicht erfasst",
                ["noDetails"] = "Keine Ereignisdetails aufbewahrt",
                ["part"] = "Teil {0} von {1}",
                ["continued"] = "Fortsetzung",
                ["version"] = "Version",
                ["page"] = "Seite",
                ["of"] = "von",
                ["footer"] = "RefTest Management — Export personenbezogener Daten"
            }
        };
    }

    private static Dictionary<string, Dictionary<string, string>> InitializePdfReportTranslations()
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["Report"] = "RefTest Report",
                ["Version"] = "Version",
                ["Title"] = "Title",
                ["First Name"] = "First Name",
                ["Last Name"] = "Last Name",
                ["Started At"] = "Started At",
                ["Completed At"] = "Completed At",
                ["Duration"] = "Duration",
                ["Language"] = "Language",
                ["Q Score"] = "Q Score",
                ["Q Total"] = "Q Total",
                ["A Score"] = "A Score",
                ["A Total"] = "A Total",
                ["Percentage"] = "%",
                ["Passed"] = "Passed",
                ["Yes"] = "Yes",
                ["No"] = "No",
                ["Generated"] = "Generated",
                ["Total RefTests"] = "Total RefTests",
                ["Page"] = "Page",
                ["of"] = "of"
            },
            ["nl"] = new()
            {
                ["Report"] = "RefTest Rapport",
                ["Version"] = "Versie",
                ["Title"] = "Titel",
                ["First Name"] = "Voornaam",
                ["Last Name"] = "Achternaam",
                ["Started At"] = "Gestart Op",
                ["Completed At"] = "Voltooid Op",
                ["Duration"] = "Duur",
                ["Language"] = "Taal",
                ["Q Score"] = "Vraag Score",
                ["Q Total"] = "Vraag Totaal",
                ["A Score"] = "Antwoord Score",
                ["A Total"] = "Antwoord Totaal",
                ["Percentage"] = "%",
                ["Passed"] = "Geslaagd",
                ["Yes"] = "Ja",
                ["No"] = "Nee",
                ["Generated"] = "Gegenereerd",
                ["Total RefTests"] = "Totaal RefTests",
                ["Page"] = "Pagina",
                ["of"] = "van"
            },
            ["fr"] = new()
            {
                ["Report"] = "Rapport RefTest",
                ["Version"] = "Version",
                ["Title"] = "Titre",
                ["First Name"] = "Prénom",
                ["Last Name"] = "Nom",
                ["Started At"] = "Commencé",
                ["Completed At"] = "Terminé",
                ["Duration"] = "Durée",
                ["Language"] = "Langue",
                ["Q Score"] = "Score Questions",
                ["Q Total"] = "Total Questions",
                ["A Score"] = "Score Réponses",
                ["A Total"] = "Total Réponses",
                ["Percentage"] = "%",
                ["Passed"] = "Réussi",
                ["Yes"] = "Oui",
                ["No"] = "Non",
                ["Generated"] = "Généré",
                ["Total RefTests"] = "RefTests Totales",
                ["Page"] = "Page",
                ["of"] = "de"
            },
            ["de"] = new()
            {
                ["Report"] = "RefTest Bericht",
                ["Version"] = "Version",
                ["Title"] = "Titel",
                ["First Name"] = "Vorname",
                ["Last Name"] = "Nachname",
                ["Started At"] = "Gestartet",
                ["Completed At"] = "Abgeschlossen",
                ["Duration"] = "Dauer",
                ["Language"] = "Sprache",
                ["Q Score"] = "Fragen Punkte",
                ["Q Total"] = "Fragen Gesamt",
                ["A Score"] = "Antworten Punkte",
                ["A Total"] = "Antworten Gesamt",
                ["Percentage"] = "%",
                ["Passed"] = "Bestanden",
                ["Yes"] = "Ja",
                ["No"] = "Nein",
                ["Generated"] = "Erstellt",
                ["Total RefTests"] = "Gesamt RefTests",
                ["Page"] = "Seite",
                ["of"] = "von"
            }
        };
    }

    private static Dictionary<string, Dictionary<string, string>> InitializeReportColumnTranslations()
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["Title"] = "Title",
                ["First Name"] = "First Name",
                ["Last Name"] = "Last Name",
                ["Started At"] = "Started At",
                ["Completed At"] = "Completed At",
                ["Duration"] = "Duration",
                ["Language"] = "Language",
                ["Question Score"] = "Question Score",
                ["Question Total"] = "Question Total",
                ["Answer Score"] = "Answer Score",
                ["Answer Total"] = "Answer Total",
                ["Percentage"] = "Percentage",
                ["Passed"] = "Passed",
                ["LanguageInstruction"] = "Select language"
            },
            ["nl"] = new()
            {
                ["Title"] = "Titel",
                ["First Name"] = "Voornaam",
                ["Last Name"] = "Achternaam",
                ["Started At"] = "Gestart Op",
                ["Completed At"] = "Voltooid Op",
                ["Duration"] = "Duur",
                ["Language"] = "Taal",
                ["Question Score"] = "Vraag Score",
                ["Question Total"] = "Vraag Totaal",
                ["Answer Score"] = "Antwoord Score",
                ["Answer Total"] = "Antwoord Totaal",
                ["Percentage"] = "Percentage",
                ["Passed"] = "Geslaagd",
                ["LanguageInstruction"] = "Selecteer taal"
            },
            ["fr"] = new()
            {
                ["Title"] = "Titre",
                ["First Name"] = "Prénom",
                ["Last Name"] = "Nom",
                ["Started At"] = "Commencé À",
                ["Completed At"] = "Terminé À",
                ["Duration"] = "Durée",
                ["Language"] = "Langue",
                ["Question Score"] = "Score Questions",
                ["Question Total"] = "Total Questions",
                ["Answer Score"] = "Score Réponses",
                ["Answer Total"] = "Total Réponses",
                ["Percentage"] = "Pourcentage",
                ["Passed"] = "Réussi",
                ["LanguageInstruction"] = "Sélectionner la langue"
            },
            ["de"] = new()
            {
                ["Title"] = "Titel",
                ["First Name"] = "Vorname",
                ["Last Name"] = "Nachname",
                ["Started At"] = "Gestartet Um",
                ["Completed At"] = "Abgeschlossen Um",
                ["Duration"] = "Dauer",
                ["Language"] = "Sprache",
                ["Question Score"] = "Fragen-Punktzahl",
                ["Question Total"] = "Fragen Gesamt",
                ["Answer Score"] = "Antworten-Punktzahl",
                ["Answer Total"] = "Antworten Gesamt",
                ["Percentage"] = "Prozentsatz",
                ["Passed"] = "Bestanden",
                ["LanguageInstruction"] = "Sprache wählen"
            }
        };
    }
}
