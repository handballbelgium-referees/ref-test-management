import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-quiz-header',
  templateUrl: './quiz-header.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
})
export class QuizHeaderComponent {
  readonly timeRemaining = input.required<number>();
  readonly answeredCount = input.required<number>();
  readonly totalQuestions = input.required<number>();
  readonly progress = input.required<number>();
}
