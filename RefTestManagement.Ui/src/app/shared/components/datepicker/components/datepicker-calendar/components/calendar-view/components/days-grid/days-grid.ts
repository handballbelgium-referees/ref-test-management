import {
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { CalendarDay } from '../../../../../../models/datepicker.types';
import { Datepicker } from '../../../../../../services/datepicker';

@Component({
  selector: 'app-days-grid',
  templateUrl: './days-grid.html',
  host: {
    class: 'host',
  },
})
export class DaysGrid {
  private readonly _dateService = inject(Datepicker);

  readonly currentDate = input.required<Date>();
  readonly selectedDate = input<Date | null>(null);

  protected readonly dateSelect = output<Date>();
  protected readonly currentDateChange = output<Date>();

  protected readonly today = new Date();
  protected readonly activeDate = signal<Date | null>(null);
  private readonly _grid = viewChild<ElementRef<HTMLElement>>('grid');

  protected readonly weekDays = computed(() => {
    return this._dateService.getWeekDays();
  });

  protected readonly fullWeekDays = computed(() => {
    return this._dateService.getWeekDays('long');
  });

  protected readonly gridLabel = computed(() =>
    this._dateService.getViewTitle(this.currentDate(), 'days'),
  );

  protected readonly calendarDays = computed(() => {
    return this._dateService.generateCalendarDays(
      this.currentDate(),
      this.selectedDate(),
      this.today,
    );
  });

  protected readonly calendarWeeks = computed(() => {
    const days = this.calendarDays();
    return Array.from({ length: days.length / 7 }, (_, index) =>
      days.slice(index * 7, index * 7 + 7),
    );
  });

  constructor() {
    effect(() => {
      const days = this.calendarDays();
      const active = this.activeDate();
      if (active && days.some((day) => this._dateService.isSameDay(day.date, active))) return;

      const selected = this.selectedDate();
      const current = this.currentDate();
      const initialDay =
        days.find((day) => selected && this._dateService.isSameDay(day.date, selected)) ??
        days.find((day) => this._dateService.isSameDay(day.date, current)) ??
        days.find((day) => day.isCurrentMonth) ??
        days[0];
      if (initialDay) this.activeDate.set(initialDay.date);
    });
  }

  protected isActive(day: CalendarDay): boolean {
    const active = this.activeDate();
    return !!active && this._dateService.isSameDay(day.date, active);
  }

  protected dateLabel(day: CalendarDay): string {
    return this._dateService.getDateLabel(day.date);
  }

  protected onKeydown(event: KeyboardEvent, day: CalendarDay): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.selectDay(day);
      return;
    }

    const date = new Date(day.date);
    switch (event.key) {
      case 'ArrowLeft':
        date.setDate(date.getDate() - 1);
        break;
      case 'ArrowRight':
        date.setDate(date.getDate() + 1);
        break;
      case 'ArrowUp':
        date.setDate(date.getDate() - 7);
        break;
      case 'ArrowDown':
        date.setDate(date.getDate() + 7);
        break;
      case 'Home':
        date.setDate(date.getDate() - ((date.getDay() + 6) % 7));
        break;
      case 'End':
        date.setDate(date.getDate() + (6 - ((date.getDay() + 6) % 7)));
        break;
      case 'PageUp':
      case 'PageDown': {
        const dayOfMonth = date.getDate();
        date.setDate(1);
        date.setMonth(date.getMonth() + (event.key === 'PageUp' ? -1 : 1) * (event.ctrlKey ? 12 : 1));
        date.setDate(Math.min(dayOfMonth, new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate()));
        break;
      }
      default:
        return;
    }

    event.preventDefault();
    this.activeDate.set(date);
    if (
      date.getFullYear() !== this.currentDate().getFullYear() ||
      date.getMonth() !== this.currentDate().getMonth()
    ) {
      this.currentDateChange.emit(date);
    }
    queueMicrotask(() =>
      this._grid()
        ?.nativeElement.querySelector<HTMLElement>(
          'button[data-calendar-day][tabindex="0"]',
        )
        ?.focus(),
    );
  }

  protected selectDay(day: CalendarDay): void {
    if (!day.isDisabled) {
      const date = new Date(day.date);
      date.setHours(0, 0, 0, 0);
      this.activeDate.set(date);
      this.dateSelect.emit(date);
    }
  }
}
