import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-quiz-error',
  templateUrl: './quiz-error.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
})
export class QuizErrorComponent {
  readonly errorType = input.required<string>();
}
