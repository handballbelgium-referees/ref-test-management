import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface IQuestion {
  id: string;
}

@Component({
  selector: 'app-quiz-results',
  imports: [TranslatePipe],
  templateUrl: './quiz-results.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class QuizResultsComponent {
  readonly questions = input.required<IQuestion[]>();
  readonly questionScore = input.required<number>();
  readonly questionTotal = input.required<number>();
  readonly answerScore = input.required<number>();
  readonly answerTotal = input.required<number>();
  readonly percentage = input.required<number>();
}
