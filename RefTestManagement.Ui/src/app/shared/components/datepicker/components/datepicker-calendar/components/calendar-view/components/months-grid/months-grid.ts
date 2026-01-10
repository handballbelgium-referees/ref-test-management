import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { Datepicker } from '../../../../../../services/datepicker';

@Component({
  selector: 'app-months-grid',
  templateUrl: './months-grid.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'host',
  },
})
export class MonthsGrid {
  private readonly _dateService = inject(Datepicker);

  readonly currentDate = input.required<Date>();

  readonly monthSelect = output<number>();

  protected readonly months = computed(() => {
    return this._dateService.generateMonths(this.currentDate());
  });

  protected selectMonth(monthIndex: number): void {
    this.monthSelect.emit(monthIndex);
  }
}
