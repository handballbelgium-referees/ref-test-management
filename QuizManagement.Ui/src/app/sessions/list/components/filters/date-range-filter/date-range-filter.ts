import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

export interface IDateRange {
  after?: string;
  before?: string;
}

@Component({
  selector: 'app-date-range-filter',
  imports: [TranslatePipe],
  templateUrl: './date-range-filter.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class DateRangeFilter {
  readonly label = input.required<string>();
  readonly afterDate = input<string | undefined>();
  readonly beforeDate = input<string | undefined>();

  readonly dateChange = output<IDateRange>();

  protected onAfterChange(value: string): void {
    this.dateChange.emit({ after: value || undefined });
  }

  protected onBeforeChange(value: string): void {
    this.dateChange.emit({ before: value || undefined });
  }
}
