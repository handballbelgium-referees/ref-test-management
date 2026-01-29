import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { CalendarDay } from '../../../../../../models/datepicker.types';
import { Datepicker } from '../../../../../../services/datepicker';

@Component({
  selector: 'app-days-grid',
  templateUrl: './days-grid.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'host',
  },
})
export class DaysGrid {
  private readonly _dateService = inject(Datepicker);

  readonly currentDate = input.required<Date>();
  readonly selectedDate = input<Date | null>(null);

  protected readonly dateSelect = output<Date>();

  protected readonly today = new Date();

  protected readonly weekDays = computed(() => {
    return this._dateService.getWeekDays();
  });

  protected readonly calendarDays = computed(() => {
    return this._dateService.generateCalendarDays(
      this.currentDate(),
      this.selectedDate(),
      this.today,
    );
  });

  protected selectDay(day: CalendarDay): void {
    if (!day.isDisabled) {
      const date = new Date(day.date);
      date.setHours(0, 0, 0, 0);
      this.dateSelect.emit(date);
    }
  }
}
