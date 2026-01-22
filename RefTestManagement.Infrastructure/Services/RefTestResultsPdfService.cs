using Handball.Belgium.RefTestManagement.Application.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public interface IRefTestResultsPdfService
{
    byte[] GenerateRefTestResultsPdf(
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
        List<Question> questionsWithCorrectAnswers);
}

public class RefTestResultsPdfService(ILogoService logoService, ITranslationService translationService) : IRefTestResultsPdfService
{

    public byte[] GenerateRefTestResultsPdf(
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
        // Download logo synchronously for PDF generation
        var logo = logoService.GetLogoBytesAsync().GetAwaiter().GetResult();
        
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
                    column.Item().Element(c => ComposeHeader(c, name, language, questionScore, answerScore,
                        totalQuestions, answerTotal, percentage, logo));

                    // Content
                    column.Item().Element(c => ComposeContent(c, language, questionsWithCorrectAnswers,
                        selectedAnswerIds,
                        wrongQuestionIds, wrongAnswerIds));
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Referees Handball Belgium - RefTest Results").FontSize(9)
                        .FontColor(Colors.Grey.Darken2);
                });
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container, string name, string language, int questionScore,
        int answerScore, int totalQuestions, int answerTotal, double percentage, byte[]? logo)
    {
        var translations = translationService.GetPdfResultsTranslations(language);
        
        container.Column(column =>
        {
            // Title section with rounded corners using SVG
            column.Item().Layers(layers =>
            {
                layers.Layer().ExtendVertical().Svg(size =>
                    $@"<svg width=""{size.Width}"" height=""{size.Height}"" xmlns=""http://www.w3.org/2000/svg"">
                        <rect width=""{size.Width}"" height=""{size.Height}"" rx=""12"" ry=""12"" fill=""#b30510""/>
                    </svg>");

                layers.PrimaryLayer().Padding(20).Row(titleRow =>
                {
                    // Add logo if available - place it on the left
                    if (logo != null)
                    {
                        titleRow.ConstantItem(60).AlignMiddle().Height(50).Image(logo);
                        titleRow.ConstantItem(15); // Spacing between logo and text
                    }

                    // Text content on the right
                    titleRow.RelativeItem().AlignMiddle().Column(textColumn =>
                    {
                        textColumn.Item().AlignLeft().Text("RefTest").FontSize(24).Bold().FontColor(Colors.White);
                        textColumn.Item().AlignLeft().Text(translations["resultsTitle"]).FontSize(14)
                            .FontColor("#fecaca");
                    });
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

    private void ComposeContent(IContainer container, string language, List<Question> questions,
        List<string> selectedAnswerIds, List<string> wrongQuestionIds, List<string> wrongAnswerIds)
    {
        var translations = translationService.GetPdfResultsTranslations(language);

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
                                row.ConstantItem(20).AlignMiddle().Width(14).Height(14).Svg(_ => isQuestionCorrect
                                    ?
                                    // Checkmark SVG
                                    @"<svg width=""14"" height=""14"" viewBox=""0 0 24 24"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
                                                <path d=""M20 6L9 17L4 12"" stroke=""#16a34a"" stroke-width=""3"" stroke-linecap=""round"" stroke-linejoin=""round""/>
                                            </svg>"
                                    :
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
                                    ? answerValue
                                    : string.Empty;
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
                                            // Icon (20px wide)
                                            answerRow.ConstantItem(20).AlignMiddle().Width(12).Height(12).Svg(_ =>
                                            {
                                                string svgIcon;
                                                if (isUserSelected && isWrongAnswer)
                                                {
                                                    // Red X/Cross for wrong answer
                                                    svgIcon =
                                                        @"<svg width=""12"" height=""12"" viewBox=""0 0 24 24"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
                                                                    <circle cx=""12"" cy=""12"" r=""11"" fill=""#dc2626""/>
                                                                    <path d=""M16 8L8 16M8 8L16 16"" stroke=""white"" stroke-width=""2.5"" stroke-linecap=""round"" stroke-linejoin=""round""/>
                                                                </svg>";
                                                }
                                                else if (isUserSelected && !isWrongAnswer)
                                                {
                                                    // Green checkmark for the correct user answer
                                                    svgIcon =
                                                        @"<svg width=""12"" height=""12"" viewBox=""0 0 24 24"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
                                                                    <circle cx=""12"" cy=""12"" r=""11"" fill=""#16a34a""/>
                                                                    <path d=""M17 8L10 16L6 12"" stroke=""white"" stroke-width=""2.5"" stroke-linecap=""round"" stroke-linejoin=""round""/>
                                                                </svg>";
                                                }
                                                else if (isCorrectAnswer)
                                                {
                                                    // Green checkmark (no circle) for correct answer not selected
                                                    svgIcon =
                                                        @"<svg width=""12"" height=""12"" viewBox=""0 0 24 24"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
                                                                    <path d=""M20 6L9 17L4 12"" stroke=""#16a34a"" stroke-width=""3"" stroke-linecap=""round"" stroke-linejoin=""round""/>
                                                                </svg>";
                                                }
                                                else
                                                {
                                                    // Empty for non-selected, non-correct answers
                                                    svgIcon =
                                                        @"<svg width=""12"" height=""12"" viewBox=""0 0 24 24"" fill=""none"" xmlns=""http://www.w3.org/2000/svg""></svg>";
                                                }

                                                return svgIcon;
                                            });

                                            // Answer text
                                            answerRow.RelativeItem().AlignMiddle().Text(text =>
                                            {
                                                var mainSpan = text.Span($"{answer.Number}) {answerText}").FontSize(9);

                                                if (isUserSelected)
                                                {
                                                    mainSpan.Bold();
                                                    mainSpan.FontColor(isWrongAnswer ? "#dc2626" : "#16a34a");
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
}