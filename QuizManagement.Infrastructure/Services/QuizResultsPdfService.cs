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
        int totalQuestions,
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
                    column.Item().Element(c => ComposeHeader(c, name, language, totalQuestions));
                    
                    // Content
                    column.Item().Element(c => ComposeContent(c, language, questionsWithCorrectAnswers, selectedAnswerIds,
                        wrongQuestionIds, wrongAnswerIds));
                });
                
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Referees Handball Belgium - IHF Rules Quiz Results").FontSize(9)
                        .FontColor(Colors.Grey.Darken2);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, string name, string language, int totalQuestions)
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
                    titleColumn.Item().AlignCenter().Text("IHF Rules Quiz").FontSize(24).Bold().FontColor(Colors.White);
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
                        text.Span(translations["totalQuestions"] + ": ").Bold().FontSize(11);
                        text.Span($"{totalQuestions}").FontSize(11);
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

                column.Item().PaddingBottom(16).Element(cardContainer =>
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
                            row.ConstantItem(20).Text(text =>
                            {
                                text.Span(isQuestionCorrect ? "✓" : "✗")
                                    .FontSize(14)
                                    .FontColor(isQuestionCorrect ? "#16a34a" : "#dc2626");
                            });

                            row.RelativeItem().Text(text =>
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

                        // Answers
                        foreach (var answer in question.Answers)
                        {
                            var answerText = answer.Phrase.TryGetValue(language, out var answerValue)
                                ? answerValue
                                : string.Empty;
                            var isUserSelected = selectedAnswerIds.Contains(answer.Id);
                            var isWrongAnswer = wrongAnswerIds.Contains(answer.Id);
                            var isCorrectAnswer = answer.IsCorrect;

                            string textColor;
                            var icon = "";
                            bool bold;

                            switch (isUserSelected)
                            {
                                case true when isWrongAnswer:
                                    // User selected the wrong answer
                                    textColor = "#dc2626"; // red-600
                                    icon = "✗";
                                    bold = true;
                                    break;
                                case true when !isWrongAnswer:
                                    // User selected the correct answer
                                    textColor = "#16a34a"; // green-600
                                    icon = "✓";
                                    bold = true;
                                    break;
                                default:
                                {
                                    if (isCorrectAnswer)
                                    {
                                        // Correct answer (not selected by the user)
                                        textColor = "#16a34a"; // green-600
                                        icon = "✓";
                                    }
                                    else
                                    {
                                        // Other answers
                                        textColor = "#737373"; // neutral-500
                                    }

                                    bold = false;

                                    break;
                                }
                            }

                            questionColumn.Item().PaddingTop(3).Row(row =>
                            {
                                if (!string.IsNullOrEmpty(icon))
                                {
                                    row.ConstantItem(15).Text(icon).FontSize(10).FontColor(textColor);
                                }
                                else
                                {
                                    row.ConstantItem(15);
                                }

                                row.RelativeItem().Text(text =>
                                {
                                    var span = text.Span($"{answer.Number}: {answerText}")
                                        .FontSize(9)
                                        .FontColor(textColor);

                                    if (bold)
                                    {
                                        span.Bold();
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
                ["totalQuestions"] = "Totaal aantal vragen",
                ["reviewAnswers"] = "Antwoorden Beoordelen",
                ["question"] = "Vraag"
            },
            "fr" => new Dictionary<string, string>
            {
                ["resultsTitle"] = "Vos Résultats",
                ["name"] = "Nom",
                ["totalQuestions"] = "Total des questions",
                ["reviewAnswers"] = "Réviser les Réponses",
                ["question"] = "Question"
            },
            "de" => new Dictionary<string, string>
            {
                ["resultsTitle"] = "Ihre Ergebnisse",
                ["name"] = "Name",
                ["totalQuestions"] = "Gesamtzahl der Fragen",
                ["reviewAnswers"] = "Antworten Überprüfen",
                ["question"] = "Frage"
            },
            _ => new Dictionary<string, string> // "en" (default)
            {
                ["resultsTitle"] = "Your Results",
                ["name"] = "Name",
                ["totalQuestions"] = "Total Questions",
                ["reviewAnswers"] = "Review Answers",
                ["question"] = "Question"
            }
        };
    }
}