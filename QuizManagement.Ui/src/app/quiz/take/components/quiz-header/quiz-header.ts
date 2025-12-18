import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-quiz-header',
  imports: [TranslatePipe],
  templateUrl: './quiz-header.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class QuizHeaderComponent {
  readonly timeRemaining = input.required<string>();
  readonly timeRemainingSeconds = input.required<number>();
  readonly answeredCount = input.required<number>();
  readonly totalQuestions = input.required<number>();
  readonly progress = input.required<number>();
}
