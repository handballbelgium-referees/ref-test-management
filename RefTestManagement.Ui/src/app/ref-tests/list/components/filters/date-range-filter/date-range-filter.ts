import { Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Datepicker } from '../../../../../shared/components/datepicker/datepicker';

export interface IDateRange {
  after?: string;
  before?: string;
}

@Component({
  selector: 'app-date-range-filter',
  imports: [TranslatePipe, Datepicker],
  templateUrl: './date-range-filter.html',
  host: {
    class: 'block',
  },
})
export class DateRangeFilter {
  readonly label = input.required<string>();
  readonly afterDate = input<string | undefined>();
  readonly beforeDate = input<string | undefined>();

  protected readonly dateChange = output<IDateRange>();

  protected onAfterChange(value: string): void {
    this.dateChange.emit({
      after: value ?? undefined,
      before: this.beforeDate(),
    });
  }

  protected onBeforeChange(value: string): void {
    this.dateChange.emit({
      after: this.afterDate(),
      before: value ?? undefined,
    });
  }
}
