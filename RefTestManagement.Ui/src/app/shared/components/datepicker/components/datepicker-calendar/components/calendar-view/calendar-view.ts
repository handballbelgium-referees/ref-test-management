import { Component, input, output } from '@angular/core';
import { ViewMode } from '../../../../models/datepicker.types';
import { DaysGrid } from './components/days-grid/days-grid';
import { MonthsGrid } from './components/months-grid/months-grid';
import { YearsGrid } from './components/years-grid/years-grid';

@Component({
  selector: 'app-calendar-view',
  imports: [DaysGrid, MonthsGrid, YearsGrid],
  templateUrl: './calendar-view.html',
  host: {
    class: 'block',
  },
})
export class CalendarView {
  readonly viewMode = input.required<ViewMode>();
  readonly currentDate = input.required<Date>();
  readonly selectedDate = input<Date | null>(null);

  protected readonly dateSelect = output<Date>();
  protected readonly monthSelect = output<number>();
  protected readonly yearSelect = output<number>();
}
