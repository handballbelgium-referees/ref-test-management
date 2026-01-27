import { ChangeDetectionStrategy, Component, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-submit-ref-test-dialog',
  imports: [TranslatePipe],
  templateUrl: './submit-ref-test-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class SubmitRefTestDialog {
  readonly show = input.required<boolean>();
  readonly answeredCount = input.required<number>();
  readonly totalQuestions = input.required<number>();

  protected readonly confirm = output<void>();
  protected readonly cancel = output<void>();

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
