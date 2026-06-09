import { Component, computed, inject, input, output } from '@angular/core';
import { Datepicker } from '../../../../../../services/datepicker';

@Component({
  selector: 'app-years-grid',
  templateUrl: './years-grid.html',
  host: {
    class: 'host',
  },
})
export class YearsGrid {
  private readonly _dateService = inject(Datepicker);

  readonly currentDate = input.required<Date>();

  protected readonly yearSelect = output<number>();

  protected readonly years = computed(() => {
    return this._dateService.generateYears(this.currentDate());
  });

  protected selectYear(year: number): void {
    this.yearSelect.emit(year);
  }
}
