import { ChangeDetectionStrategy, Component, computed, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface IResultsSummary {
  newResults: Array<{ name: string; email: string }>;
  resendResults: Array<{ name: string; email: string }>;
}

@Component({
  selector: 'app-send-results-dialog',
  imports: [TranslatePipe],
  templateUrl: './send-results-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'host',
  },
})
export class SendResultsDialog {
  readonly show = input.required<boolean>();
  readonly summary = input.required<IResultsSummary>();

  readonly confirm = output<void>();
  readonly cancel = output<void>();

  protected readonly totalCount = computed(() => {
    const summary = this.summary();
    return summary.newResults.length + summary.resendResults.length;
  });

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
