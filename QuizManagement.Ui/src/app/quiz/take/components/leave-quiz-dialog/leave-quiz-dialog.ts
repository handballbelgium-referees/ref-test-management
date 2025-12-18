import { ChangeDetectionStrategy, Component, effect, input, output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-leave-quiz-dialog',
  templateUrl: './leave-quiz-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslateModule],
  host: {
    class: 'block',
  },
})
export class LeaveQuizDialogComponent {
  readonly show = input.required<boolean>();
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
