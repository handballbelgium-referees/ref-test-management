import { CommonModule } from '@angular/common';
import {
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';

interface CalendarDay {
  date: Date;
  day: number;
  isCurrentMonth: boolean;
  isToday: boolean;
  isSelected: boolean;
  isDisabled: boolean;
}

type ViewMode = 'days' | 'months' | 'years';

@Component({
  selector: 'app-datepicker',
  imports: [CommonModule, TranslateModule],
  templateUrl: './datepicker.html',
  styleUrl: './datepicker.css',
  host: {
    '[attr.tabindex]': '"-1"',
    '(document:keydown.escape)': 'onEscape()',
  },
})
export class Datepicker {
  private readonly _translate = inject(TranslateService);
  private readonly _elRef = inject(ElementRef);
  private readonly _destroyRef = inject(DestroyRef);
  private _clickListener?: (event: MouseEvent) => void;
  private _scrollListener?: () => void;

  readonly value = input<string>('');
  readonly dateChange = output<string>();
  readonly placeholder = input<string>('dd/mm/yyyy');

  readonly calendar = viewChild<ElementRef>('calendar');
  readonly inputElement = viewChild<ElementRef>('input');

  readonly currentDate = signal<Date>(new Date());
  protected readonly viewMode = signal<ViewMode>('days');
  protected readonly today = new Date();
  protected readonly isOpen = signal<boolean>(false);
  protected readonly displayValue = signal<string>('');

  private readonly _selectedDate = signal<Date | null>(null);

  protected readonly calendarDays = computed(() => {
    const date = this.currentDate();
    const year = date.getFullYear();
    const month = date.getMonth();
    const selectedDate = this._selectedDate();

    const firstDay = new Date(year, month, 1);
    const lastDay = new Date(year, month + 1, 0);
    const prevLastDay = new Date(year, month, 0);

    const firstDayOfWeek = firstDay.getDay() === 0 ? 6 : firstDay.getDay() - 1;
    const lastDate = lastDay.getDate();
    const prevLastDate = prevLastDay.getDate();

    const days: CalendarDay[] = [];

    // Previous month days
    for (let i = firstDayOfWeek; i > 0; i--) {
      const date = new Date(year, month - 1, prevLastDate - i + 1);
      days.push({
        date,
        day: date.getDate(),
        isCurrentMonth: false,
        isToday: this.isSameDay(date, this.today),
        isSelected: selectedDate ? this.isSameDay(date, selectedDate) : false,
        isDisabled: false,
      });
    }

    // Current month days
    for (let day = 1; day <= lastDate; day++) {
      const date = new Date(year, month, day);
      days.push({
        date,
        day,
        isCurrentMonth: true,
        isToday: this.isSameDay(date, this.today),
        isSelected: selectedDate ? this.isSameDay(date, selectedDate) : false,
        isDisabled: false,
      });
    }

    // Next month days to complete the last week only
    const totalDays = days.length;
    const weeksNeeded = Math.ceil(totalDays / 7);
    const remainingDays = weeksNeeded * 7 - totalDays;

    for (let day = 1; day <= remainingDays; day++) {
      const date = new Date(year, month + 1, day);
      days.push({
        date,
        day,
        isCurrentMonth: false,
        isToday: this.isSameDay(date, this.today),
        isSelected: selectedDate ? this.isSameDay(date, selectedDate) : false,
        isDisabled: false,
      });
    }

    return days;
  });

  protected readonly months = computed(() => {
    const monthNames = [];
    for (let i = 0; i < 12; i++) {
      const date = new Date(2000, i, 1);
      monthNames.push({
        index: i,
        name: date.toLocaleDateString(this._translate.getCurrentLang(), { month: 'short' }),
        isSelected: i === this.currentDate().getMonth(),
      });
    }
    return monthNames;
  });

  protected readonly years = computed(() => {
    const currentYear = this.currentDate().getFullYear();
    const startYear = Math.floor(currentYear / 10) * 10;
    const years = [];

    for (let i = startYear - 1; i < startYear + 11; i++) {
      years.push({
        year: i,
        isSelected: i === currentYear,
        isOutOfRange: i < startYear || i >= startYear + 10,
      });
    }

    return years;
  });

  protected readonly viewTitle = computed(() => {
    const date = this.currentDate();
    const mode = this.viewMode();

    if (mode === 'days') {
      return date.toLocaleDateString(this._translate.getCurrentLang(), {
        month: 'long',
        year: 'numeric',
      });
    } else if (mode === 'months') {
      return date.getFullYear().toString();
    } else {
      const startYear = Math.floor(date.getFullYear() / 10) * 10;
      return `${startYear} - ${startYear + 9}`;
    }
  });

  protected readonly weekDays = computed(() => {
    const days = [];
    const baseDate = new Date(2024, 0, 1); // Monday, January 1, 2024

    for (let i = 0; i < 7; i++) {
      const date = new Date(baseDate);
      date.setDate(baseDate.getDate() + i);
      days.push(date.toLocaleDateString(this._translate.getCurrentLang(), { weekday: 'short' }));
    }

    return days;
  });

  constructor() {
    // Initialize from input value
    effect(() => {
      const initialValue = this.value();
      if (initialValue) {
        const date = new Date(initialValue);
        if (!isNaN(date.getTime())) {
          this._selectedDate.set(date);
          this.displayValue.set(this.formatDate(date));
          this.currentDate.set(new Date(date));
        }
      }
    });

    // Add click listener after a delay to prevent immediate closure
    setTimeout(() => {
      this._clickListener = (event: MouseEvent) => {
        const target = event.target as Node;
        const calendar = this.calendar();

        // Don't close if clicking inside the component or calendar
        if (
          this._elRef.nativeElement.contains(target) ||
          (calendar && calendar.nativeElement.contains(target))
        ) {
          return;
        }

        this.isOpen.set(false);
      };
      document.addEventListener('click', this._clickListener);

      this._destroyRef.onDestroy(() => {
        if (this._clickListener) {
          document.removeEventListener('click', this._clickListener);
        }
      });
    }, 300);

    // Add scroll listener to close calendar when scrolling
    this._scrollListener = () => {
      if (this.isOpen()) {
        this.isOpen.set(false);
      }
    };
    window.addEventListener('scroll', this._scrollListener, true);

    this._destroyRef.onDestroy(() => {
      if (this._scrollListener) {
        window.removeEventListener('scroll', this._scrollListener, true);
      }
    });
  }

  protected onEscape(): void {
    this.isOpen.set(false);
  }

  protected onInputFocus(): void {
    if (!this.isOpen()) {
      this.isOpen.set(true);
      this.positionCalendar();
    }
  }

  protected onInputClick(): void {
    if (!this.isOpen()) {
      this.isOpen.set(true);
      this.positionCalendar();
    }
  }

  private positionCalendar(): void {
    // Position the calendar below the input
    setTimeout(() => {
      const inputElement = this.inputElement();
      const calendarElement = this.calendar();

      if (inputElement && calendarElement) {
        const inputRect = inputElement.nativeElement.getBoundingClientRect();
        const calendarEl = calendarElement.nativeElement as HTMLElement;

        calendarEl.style.top = `${inputRect.bottom + 4}px`;
        calendarEl.style.left = `${inputRect.left}px`;
      }
    });
  }

  protected onInputKeydown(event: KeyboardEvent): void {
    const key = event.key;

    // Allow: backspace, delete, tab, escape, enter
    if (['Backspace', 'Delete', 'Tab', 'Escape', 'Enter'].includes(key)) {
      return;
    }

    // Allow: Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+X
    if (event.ctrlKey && ['a', 'c', 'v', 'x'].includes(key.toLowerCase())) {
      return;
    }

    // Allow: home, end, left, right arrows
    if (['Home', 'End', 'ArrowLeft', 'ArrowRight'].includes(key)) {
      return;
    }

    // Ensure that it is a number (0-9) or slash
    if (!/^[0-9/]$/.test(key)) {
      event.preventDefault();
    }
  }

  protected onInputChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.displayValue.set(input.value);
  }

  protected onInputBlur(event: Event): void {
    // Close calendar when input loses focus
    this.isOpen.set(false);

    const input = event.target as HTMLInputElement;
    const value = input.value;

    if (!value) {
      const currentSelected = this._selectedDate();
      if (currentSelected !== null) {
        this._selectedDate.set(null);
        this.displayValue.set('');
        this.dateChange.emit('');
      }
      return;
    }

    // Parse dd/mm/yyyy format
    const parts = value.split('/');
    if (parts.length === 3) {
      const day = parseInt(parts[0], 10);
      const month = parseInt(parts[1], 10) - 1;
      const year = parseInt(parts[2], 10);

      const date = new Date(year, month, day);
      if (!isNaN(date.getTime()) && date.getDate() === day && date.getMonth() === month) {
        date.setHours(0, 0, 0, 0);

        // Only emit if date changed
        const currentSelected = this._selectedDate();
        const isDifferent = !currentSelected || !this.isSameDay(date, currentSelected);

        this._selectedDate.set(date);
        this.currentDate.set(new Date(date));
        this.displayValue.set(this.formatDate(date));

        if (isDifferent) {
          this.dateChange.emit(date.toISOString());
        }
      } else {
        // Invalid date, revert to last valid date
        const selected = this._selectedDate();
        this.displayValue.set(selected ? this.formatDate(selected) : '');
      }
    } else if (value) {
      // Incomplete or malformed input, revert to last valid date
      const selected = this._selectedDate();
      this.displayValue.set(selected ? this.formatDate(selected) : '');
    }
  }

  protected selectDay(day: CalendarDay): void {
    if (!day.isDisabled) {
      const date = new Date(day.date);
      date.setHours(0, 0, 0, 0);

      // Only emit if the date actually changed
      const currentSelected = this._selectedDate();
      const isDifferent = !currentSelected || !this.isSameDay(date, currentSelected);

      this._selectedDate.set(date);
      this.displayValue.set(this.formatDate(date));

      if (isDifferent) {
        this.dateChange.emit(date.toISOString());
      }

      this.isOpen.set(false);
    }
  }

  protected selectMonth(monthIndex: number): void {
    const newDate = new Date(this.currentDate());
    newDate.setMonth(monthIndex);
    this.currentDate.set(newDate);
    this.viewMode.set('days');
  }

  protected selectYear(year: number): void {
    const newDate = new Date(this.currentDate());
    newDate.setFullYear(year);
    this.currentDate.set(newDate);
    this.viewMode.set('months');
  }

  protected previousMonth(): void {
    const newDate = new Date(this.currentDate());
    newDate.setMonth(newDate.getMonth() - 1);
    this.currentDate.set(newDate);
  }

  protected nextMonth(): void {
    const newDate = new Date(this.currentDate());
    newDate.setMonth(newDate.getMonth() + 1);
    this.currentDate.set(newDate);
  }

  protected previousYear(): void {
    const newDate = new Date(this.currentDate());
    newDate.setFullYear(newDate.getFullYear() - 1);
    this.currentDate.set(newDate);
  }

  protected nextYear(): void {
    const newDate = new Date(this.currentDate());
    newDate.setFullYear(newDate.getFullYear() + 1);
    this.currentDate.set(newDate);
  }

  protected previousDecade(): void {
    const newDate = new Date(this.currentDate());
    newDate.setFullYear(newDate.getFullYear() - 10);
    this.currentDate.set(newDate);
  }

  protected nextDecade(): void {
    const newDate = new Date(this.currentDate());
    newDate.setFullYear(newDate.getFullYear() + 10);
    this.currentDate.set(newDate);
  }

  protected toggleView(): void {
    const mode = this.viewMode();
    if (mode === 'days') {
      this.viewMode.set('months');
    } else if (mode === 'months') {
      this.viewMode.set('years');
    }
  }

  protected selectToday(): void {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    this._selectedDate.set(today);
    this.currentDate.set(today);
    this.viewMode.set('days');
    this.displayValue.set(this.formatDate(today));
    this.dateChange.emit(today.toISOString());
    this.isOpen.set(false);
  }

  protected clear(): void {
    this._selectedDate.set(null);
    this.displayValue.set('');
    this.dateChange.emit('');
    this.isOpen.set(false);
  }

  private formatDate(date: Date): string {
    const day = String(date.getDate()).padStart(2, '0');
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const year = date.getFullYear();
    return `${day}/${month}/${year}`;
  }

  private isSameDay(date1: Date, date2: Date): boolean {
    return (
      date1.getFullYear() === date2.getFullYear() &&
      date1.getMonth() === date2.getMonth() &&
      date1.getDate() === date2.getDate()
    );
  }
}
