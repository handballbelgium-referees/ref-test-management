import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-quiz-instructions',
  imports: [TranslatePipe],
  templateUrl: './quiz-instructions.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class QuizInstructionsComponent {
  readonly hasTimeLimit = input.required<boolean>();
}
