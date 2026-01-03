using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuizManagement.Application.Models;

namespace QuizManagement.Infrastructure.Services;

public class QuizResultsPdfService : IQuizResultsPdfService
{
    public byte[] GenerateQuizResultsPdf(
        string name,
        string language,
        int questionScore,
        int answerScore,
        int totalQuestions,
        int answerTotal,
        double percentage,
        List<string> selectedAnswerIds,
        List<string> wrongQuestionIds,
        List<string> wrongAnswerIds,
        List<Question> questionsWithCorrectAnswers)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Content().Column(column =>
                {
                    // Header only on the first page
                    column.Item().Element(c => ComposeHeader(c, name, language, questionScore, answerScore, totalQuestions, answerTotal, percentage));
                    
                    // Content
                    column.Item().Element(c => ComposeContent(c, language, questionsWithCorrectAnswers, selectedAnswerIds,
                        wrongQuestionIds, wrongAnswerIds));
                });
                
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Referees Handball Belgium - IHF Rules RefTest Results").FontSize(9)
                        .FontColor(Colors.Grey.Darken2);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, string name, string language, int questionScore,
        int answerScore, int totalQuestions, int answerTotal, double percentage)
    {
        var translations = GetTranslations(language);

        container.Column(column =>
        {
            // Title section with rounded corners using SVG
            column.Item().Layers(layers =>
            {
                layers.Layer().ExtendVertical().Svg(size => 
                    $@"<svg width=""{size.Width}"" height=""{size.Height}"" xmlns=""http://www.w3.org/2000/svg"">
                        <rect width=""{size.Width}"" height=""{size.Height}"" rx=""12"" ry=""12"" fill=""#b30510""/>
                    </svg>");
                
                layers.PrimaryLayer().Padding(20).Column(titleColumn =>
                {
                    titleColumn.Item().AlignCenter().Text("IHF Rules RefTest").FontSize(24).Bold().FontColor(Colors.White);
                    titleColumn.Item().AlignCenter().Text(translations["resultsTitle"]).FontSize(14).FontColor("#fecaca");
                });
            });

            column.Item().PaddingVertical(10);

            // Name and info section with rounded corners using SVG
            column.Item().Layers(layers =>
            {
                layers.Layer().ExtendVertical().Svg(size =>
                    $@"<svg width=""{size.Width}"" height=""{size.Height}"" xmlns=""http://www.w3.org/2000/svg"">
                        <rect width=""{size.Width}"" height=""{size.Height}"" rx=""8"" ry=""8"" fill=""#f5f5f5""/>
                    </svg>");
                
                layers.PrimaryLayer().Padding(15).Column(infoColumn =>
                {
                    infoColumn.Item().Text(text =>
                    {
                        text.Span(translations["name"] + ": ").Bold().FontSize(11);
                        text.Span(name).FontSize(11);
                    });

                    infoColumn.Item().PaddingTop(4).Text(text =>
                    {
                        text.Span(translations["percentage"] + ": ").Bold().FontSize(11);
                        text.Span($"{percentage:F2} %").FontSize(11);
                    });

                    infoColumn.Item().PaddingTop(4).Text(text =>
                    {

                        text.Span(translations["score"] + ": ").Bold().FontSize(11);
                        text.Span($"{translations["questions"]}: {questionScore} / {totalQuestions} \t\t\t");
                        text.Span($"{translations["answers"]}: {answerScore} / {answerTotal}");
                    });
                });
            });

            column.Item().PaddingVertical(10);
        });
    }

    private static void ComposeContent(IContainer container, string language, List<Question> questions,
        List<string> selectedAnswerIds, List<string> wrongQuestionIds, List<string> wrongAnswerIds)
    {
        var translations = GetTranslations(language);

        container.Column(column =>
        {
            column.Item().PaddingBottom(10).Text(translations["reviewAnswers"]).FontSize(16).Bold()
                .FontColor("#404040");

            foreach (var question in questions)
            {
                var isQuestionCorrect = !wrongQuestionIds.Contains(question.Id);
                var questionText = question.Phrase.TryGetValue(language, out var questionValue) 
                    ? questionValue : string.Empty;

                column.Item().PaddingBottom(16).ShowEntire().Element(cardContainer =>
                {
                    cardContainer.Layers(layers =>
                    {
                        // Border layer with rounded corners
                        layers.Layer().Svg(size =>
                            $"<svg width='{size.Width}' height='{size.Height}' xmlns='http://www.w3.org/2000/svg'>" +
                            $"<rect width='{size.Width}' height='{size.Height}' rx='8' ry='8' fill='{(isQuestionCorrect ? "#bbf7d0" : "#fecaca")}'/>" +
                            $"</svg>");
                        
                        // Background layer with rounded corners (inset for border effect)
                        layers.Layer().Padding(2).Svg(size =>
                            $"<svg width='{size.Width}' height='{size.Height}' xmlns='http://www.w3.org/2000/svg'>" +
                            $"<rect width='{size.Width}' height='{size.Height}' rx='7' ry='7' fill='{(isQuestionCorrect ? "#f0fdf4" : "#fef2f2")}'/>" +
                            $"</svg>");
                        
                        // Content layer
                        layers.PrimaryLayer().Padding(14).Column(questionColumn =>
                        {
                            // Question header with icon
                            questionColumn.Item().Row(row =>
                                {
                                    row.ConstantItem(20).AlignMiddle().Width(14).Height(14).Svg(_ => isQuestionCorrect ?
                                        // Checkmark SVG
                                        @"<svg width=""14"" height=""14"" viewBox=""0 0 24 24"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
                                                <path d=""M20 6L9 17L4 12"" stroke=""#16a34a"" stroke-width=""3"" stroke-linecap=""round"" stroke-linejoin=""round""/>
                                            </svg>" :
                                        // X/Cross SVG
                                        @"<svg width=""14"" height=""14"" viewBox=""0 0 24 24"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
                                                <path d=""M18 6L6 18M6 6L18 18"" stroke=""#dc2626"" stroke-width=""3"" stroke-linecap=""round"" stroke-linejoin=""round""/>
                                            </svg>");

                                    row.RelativeItem().AlignMiddle().Text(text =>
                                    {
                                        text.Span($"{translations["question"]} {question.Number}: ")
                                            .Bold()
                                            .FontSize(10)
                                            .FontColor("#404040");
                                        text.Span(questionText)
                                            .FontSize(10)
                                            .FontColor("#404040");
                                    });
                                });

                                questionColumn.Item().PaddingTop(8);

                                // Answers - Simple and clear approach
                                foreach (var answer in question.Answers)
                                {
                                    var answerText = answer.Phrase.TryGetValue(language, out var answerValue)
                                        ? answerValue : string.Empty;
                                    var isUserSelected = selectedAnswerIds.Contains(answer.Id);
                                    var isWrongAnswer = wrongAnswerIds.Contains(answer.Id);
                                    var isCorrectAnswer = answer.IsCorrect;

                                    questionColumn.Item().PaddingTop(4).Row(row =>
                                    {
                                        // Left margin
                                        row.ConstantItem(10);

                                        // Main content
                                        row.RelativeItem().Column(col =>
                                        {
                                            col.Item().Row(answerRow =>
                                            {
                                                // Emoji/Symbol (20px wide)
                                                answerRow.ConstantItem(20).Text(text =>
                                                {
                                                    if (isUserSelected && isWrongAnswer)
                                                        text.Span("❌").FontSize(10);
                                                    else if (isUserSelected && !isWrongAnswer)
                                                        text.Span("✅").FontSize(10);
                                                    else if (isCorrectAnswer)
                                                        text.Span("✓").FontSize(10).FontColor("#16a34a").Bold();
                                                });

                                                // Answer text
                                                answerRow.RelativeItem().Text(text =>
                                                {
                                                    var mainSpan = text.Span($"{answer.Number}) {answerText}").FontSize(9);
                                                    
                                                    if (isUserSelected)
                                                    {
                                                        mainSpan.Bold();
                                                        if (isWrongAnswer)
                                                            mainSpan.FontColor("#dc2626");
                                                        else
                                                            mainSpan.FontColor("#16a34a");
                                                    }
                                                    else if (isCorrectAnswer)
                                                    {
                                                        mainSpan.FontColor("#16a34a");
                                                    }
                                                    else
                                                    {
                                                        mainSpan.FontColor("#737373");
                                                    }
                                                });
                                            });
                                            
                                            // Label underneath
                                            if (isUserSelected || isCorrectAnswer)
                                            {
                                                col.Item().PaddingLeft(20).PaddingTop(1).Text(text =>
                                                {
                                                    if (isUserSelected)
                                                    {
                                                        text.Span($"→ {translations["yourAnswer"]}")
                                                            .FontSize(7)
                                                            .Italic()
                                                            .FontColor(isWrongAnswer ? "#dc2626" : "#16a34a");
                                                    }
                                                    else if (isCorrectAnswer)
                                                    {
                                                        text.Span($"→ {translations["correctAnswer"]}")
                                                            .FontSize(7)
                                                            .Italic()
                                                            .FontColor("#16a34a");
                                                    }
                                                });
                                            }
                                        });
                                    });
                                }
                        });
                    });
                });
            }
        });
    }

    private static Dictionary<string, string> GetTranslations(string language)
    {
        return language switch
        {
            "nl" => new Dictionary<string, string>
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
            "fr" => new Dictionary<string, string>
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
            "de" => new Dictionary<string, string>
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
            },
            _ => new Dictionary<string, string> // "en" (default)
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
            }
        };
    }
}