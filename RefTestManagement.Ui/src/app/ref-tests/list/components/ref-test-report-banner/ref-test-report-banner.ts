import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

export interface ReportResult {
  success: boolean;
  refTestCount: number;
}

/**
 * Banner component to display report generation results.
 * Shows success or error state with appropriate styling.
 */
@Component({
  selector: 'app-ref-test-report-banner',
  imports: [TranslatePipe],
  templateUrl: './ref-test-report-banner.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestReportBanner {
  readonly result = input.required<ReportResult>();
  readonly dismiss = output<void>();
}
