import { Component, computed, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { IsolatedBannerManager } from '../../../../../services/banner';
import { Banner } from '../../../../../shared/components/banner/banner';

interface IResultsSummary {
  newResults: Array<{ name: string; email: string }>;
  resendResults: Array<{ name: string; email: string }>;
}

@Component({
  selector: 'app-send-results-dialog',
  imports: [TranslatePipe, Banner],
  templateUrl: './send-results-dialog.html',
  host: {
    class: 'host',
  },
})
export class SendResultsDialog {
  readonly loading = input.required<boolean>();
  readonly show = input.required<boolean>();
  readonly summary = input.required<IResultsSummary>();
  readonly bannerManager = input.required<IsolatedBannerManager>();

  protected readonly confirm = output<void>();
  protected readonly cancel = output<void>();

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
