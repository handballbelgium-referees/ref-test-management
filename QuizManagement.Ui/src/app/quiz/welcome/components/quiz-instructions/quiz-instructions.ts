import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-quiz-instructions',
  templateUrl: './quiz-instructions.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
})
export class QuizInstructionsComponent {
  readonly hasTimeLimit = input.required<boolean>();
}
