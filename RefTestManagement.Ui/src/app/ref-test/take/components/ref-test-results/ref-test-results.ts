import { Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { AnswersSummary } from '../../../../ref-tests/detail/components/ref-test-questions-tab/components/answers-summary/answers-summary';
import { QuestionCard as ReviewQuestionCard } from '../../../../ref-tests/detail/components/ref-test-questions-tab/components/question-card/question-card';
import { Question } from '../../state/ref-test.models';

@Component({
  selector: 'app-ref-test-results',
  imports: [TranslatePipe, ReviewQuestionCard, AnswersSummary],
  templateUrl: './ref-test-results.html',
  host: {
    class: 'block',
  },
})
export class RefTestResults {
  readonly questions = input.required<Question[]>();
  readonly questionScore = input.required<number>();
  readonly questionTotal = input.required<number>();
  readonly answerScore = input.required<number>();
  readonly answerTotal = input.required<number>();
  readonly percentage = input.required<number>();
  readonly passingPercentage = input.required<number>();
  readonly emailDelayMinutes = input.required<number>();
  readonly currentLanguage = input.required<string>();
  readonly selectedAnswerIds = input.required<string[]>();
  readonly sendResultsAutomatically = input.required<boolean>();
  readonly resultsSent = input.required<boolean>();

  protected readonly selectedAnswerIdsSet = computed(() => new Set(this.selectedAnswerIds()));
  protected readonly reviewQuestions = computed(() =>
    this.questions().map((question) => ({
      id: question.id,
      number: question.number ?? '',
      phrase: question.phrase,
      answers: question.answers.map((answer) => ({
        id: answer.id,
        number: answer.number ?? '',
        phrase: answer.phrase,
        isCorrect: answer.isCorrect ?? false,
      })),
    })),
  );

  protected readonly answeredQuestionsCount = computed(
    () =>
      this.questions().filter((question) =>
        question.answers.some((answer) => this.selectedAnswerIdsSet().has(answer.id)),
      ).length,
  );
}
