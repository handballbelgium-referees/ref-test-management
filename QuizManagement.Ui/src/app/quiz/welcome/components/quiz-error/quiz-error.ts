import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-quiz-error',
  imports: [TranslatePipe],
  templateUrl: './quiz-error.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class QuizErrorComponent {
  readonly errorType = input.required<string>();
}
