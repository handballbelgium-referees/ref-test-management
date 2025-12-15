import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface IQuestion {
  id: string;
  phrase: Record<string, string>;
  answers: Array<{ id: string; phrase: Record<string, string> }>;
}

@Component({
  selector: 'app-quiz-navigation',
  templateUrl: './quiz-navigation.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
})
export class QuizNavigationComponent {
  readonly questions = input.required<IQuestion[]>();
  readonly currentQuestionIndex = input.required<number>();
  readonly answeredCount = input.required<number>();
  readonly canPrevious = input.required<boolean>();
  readonly canNext = input.required<boolean>();
  readonly canSubmit = input.required<boolean>();
  readonly isLastQuestion = input.required<boolean>();
  readonly isQuestionAnswered = input.required<(questionId: string) => boolean>();
  readonly isQuestionVisited = input.required<(index: number) => boolean>();

  readonly previousClick = output<void>();
  readonly nextClick = output<void>();
  readonly submitClick = output<void>();
  readonly questionSelected = output<number>();
}
