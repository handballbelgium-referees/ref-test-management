import { Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface IQuestion {
  id: string;
  phrase: Record<string, string>;
  answers: Array<{ id: string; phrase: Record<string, string> }>;
}

@Component({
  selector: 'app-ref-test-navigation',
  imports: [TranslatePipe],
  templateUrl: './ref-test-navigation.html',
  host: {
    class: 'block',
  },
})
export class RefTestNavigation {
  readonly questions = input.required<IQuestion[]>();
  readonly currentQuestionIndex = input.required<number>();
  readonly answeredCount = input.required<number>();
  readonly canPrevious = input.required<boolean>();
  readonly canNext = input.required<boolean>();
  readonly isLastQuestion = input.required<boolean>();
  readonly isQuestionAnswered = input.required<(questionId: string) => boolean>();
  readonly isQuestionVisited = input.required<(index: number) => boolean>();

  protected readonly previousClick = output<void>();
  protected readonly nextClick = output<void>();
  protected readonly submitClick = output<void>();
  protected readonly questionSelected = output<number>();
}
