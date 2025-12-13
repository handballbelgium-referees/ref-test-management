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

                page.Header().Element(c => ComposeHeader(c, name, language, totalQuestions));
                page.Content().Element(c => ComposeContent(c, language, questionsWithCorrectAnswers, selectedAnswerIds,
                    wrongQuestionIds, wrongAnswerIds));
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
            // Title section
            column.Item().Background("#b30510").Padding(20).Column(titleColumn =>
            {
                titleColumn.Item().AlignCenter().Text("IHF Rules Quiz").FontSize(24).Bold().FontColor(Colors.White);
                titleColumn.Item().AlignCenter().Text(translations["resultsTitle"]).FontSize(14).FontColor("#fecaca");
            });

            column.Item().PaddingVertical(10);

            // Name and info section
            column.Item().Background("#f5f5f5").Padding(15).Column(infoColumn =>
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
                    ? questionValue 
                    : string.Empty;

                column.Item().PaddingBottom(8)
                    .Background(isQuestionCorrect ? "#f0fdf4" : "#fef2f2")
                    .Border(1, isQuestionCorrect ? "#bbf7d0" : "#fecaca")
                    .Padding(12)
                    .Column(questionColumn =>
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