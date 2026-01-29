import { ChangeDetectionStrategy, Component, computed, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { IsolatedBannerManager } from '../../../../../services/banner';
import { Banner } from '../../../../../shared/components/banner/banner';

interface IReportSummary {
  refTests: Array<{ name: string; email: string }>;
}

@Component({
  selector: 'app-generate-report-dialog',
  imports: [TranslatePipe, Banner],
  templateUrl: './generate-report-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'host',
  },
})
export class GenerateReportDialog {
  readonly loading = input.required<boolean>();
  readonly show = input.required<boolean>();
  readonly summary = input.required<IReportSummary>();
  readonly bannerManager = input.required<IsolatedBannerManager>();

  protected readonly confirm = output<void>();
  protected readonly cancel = output<void>();

  protected readonly totalCount = computed(() => this.summary().refTests.length);
  protected readonly displayedRefTests = computed(() => this.summary().refTests.slice(0, 10));
  protected readonly remainingCount = computed(() => {
    const total = this.totalCount();
    return total > 10 ? total - 10 : 0;
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
