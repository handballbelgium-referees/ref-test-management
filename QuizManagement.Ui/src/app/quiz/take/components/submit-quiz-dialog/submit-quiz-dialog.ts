import { ChangeDetectionStrategy, Component, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-submit-quiz-dialog',
  imports: [TranslatePipe],
  templateUrl: './submit-quiz-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class SubmitQuizDialog {
  readonly show = input.required<boolean>();
  readonly answeredCount = input.required<number>();
  readonly totalQuestions = input.required<number>();

  readonly confirm = output<void>();
  readonly cancel = output<void>();

  constructor() {
    effect(() => {
      if (this.show()) {
        document.body.style.overflow = 'hidden';
      } else {
        document.body.style.overflow = '';
      }
    });
  }
}
