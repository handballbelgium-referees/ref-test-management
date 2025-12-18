import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ViewMode } from '../../../../models/datepicker.types';
import { DaysGrid } from './components/days-grid/days-grid';
import { MonthsGrid } from './components/months-grid/months-grid';
import { YearsGrid } from './components/years-grid/years-grid';

@Component({
  selector: 'app-calendar-view',
  imports: [DaysGrid, MonthsGrid, YearsGrid],
  templateUrl: './calendar-view.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class CalendarView {
  readonly viewMode = input.required<ViewMode>();
  readonly currentDate = input.required<Date>();
  readonly selectedDate = input<Date | null>(null);

  readonly dateSelect = output<Date>();
  readonly monthSelect = output<number>();
  readonly yearSelect = output<number>();
}
