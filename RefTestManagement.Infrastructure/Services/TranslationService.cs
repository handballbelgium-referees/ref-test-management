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
    IReadOnlyDictionary<string, string> GetPdfResultsTranslations(string language);
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
    private readonly Dictionary<string, Dictionary<string, string>> _pdfResultsTranslations = InitializePdfResultsTranslations();
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

    public IReadOnlyDictionary<string, string> GetPdfResultsTranslations(string language)
    {
        return _pdfResultsTranslations.TryGetValue(language, out var translations)
            ? translations
            : _pdfResultsTranslations["en"];
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
