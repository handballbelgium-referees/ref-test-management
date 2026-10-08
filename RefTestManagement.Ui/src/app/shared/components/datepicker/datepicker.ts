import {
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { DatepickerCalendar } from './components/datepicker-calendar/datepicker-calendar';
import { DatepickerInput } from './components/datepicker-input/datepicker-input';
import { Datepicker as DatePickerService } from './services/datepicker';

let nextDatepickerId = 0;

@Component({
  selector: 'app-datepicker',
  imports: [DatepickerInput, DatepickerCalendar],
  templateUrl: './datepicker.html',
  providers: [DatePickerService],
  host: {
    '[attr.tabindex]': '"-1"',
    '(document:keydown.escape)': 'onEscape()',
  },
})
export class Datepicker {
  private readonly _dateService = inject(DatePickerService);
  private readonly _destroyRef = inject(DestroyRef);
  private _scrollListener?: () => void;
  private _resizeListener?: () => void;
  private _isRestoringFocus = false;

  readonly value = input<string>('');
  readonly placeholder = input<string>('dd/mm/yyyy');
  readonly calendarId = `datepicker-calendar-${nextDatepickerId++}`;
  protected readonly dateChange = output<string>();

  readonly label = input<string>('');

  protected readonly isOpen = signal(false);
  protected readonly displayValue = signal('');
  protected readonly currentDate = signal(new Date());
  protected readonly selectedDate = signal<Date | null>(null);
  protected readonly isSmallTouchDevice = signal(this._dateService.detectSmallTouchDevice());
  protected readonly openMode = signal<'mobile' | 'desktop'>('desktop');
  private readonly _dateInput = viewChild(DatepickerInput);
  private readonly _calendar = viewChild(DatepickerCalendar);

  constructor() {
    // Initialize from input value
    effect(() => {
      const initialValue = this.value();
      if (initialValue) {
        const date = new Date(initialValue);
        if (!isNaN(date.getTime())) {
          this.selectedDate.set(date);
          this.displayValue.set(this._dateService.formatDate(date));
          this.currentDate.set(new Date(date));
        }
      }
    });

    // Scroll listener to close calendar
    this._scrollListener = () => {
      if (this.isSmallTouchDevice()) return;

      if (this.isOpen()) {
        this.isOpen.set(false);
      }
    };

    if (this.openMode() === 'desktop') {
      window.addEventListener('scroll', this._scrollListener, true);
    }

    // Resize listener for device detection
    this._resizeListener = () => {
      this.isSmallTouchDevice.set(this._dateService.detectSmallTouchDevice());
    };
    window.addEventListener('resize', this._resizeListener);

    this._destroyRef.onDestroy(() => {
      if (this._scrollListener) {
        window.removeEventListener('scroll', this._scrollListener, true);
      }
      if (this._resizeListener) {
        window.removeEventListener('resize', this._resizeListener);
      }
    });
  }

  protected onEscape(): void {
    if (!this.isOpen()) return;
    this.isOpen.set(false);
    this.focusDateInput();
  }

  protected onInputFocus(): void {
    if (this._isRestoringFocus) return;
    this.openMode.set(this._dateService.detectSmallTouchDevice() ? 'mobile' : 'desktop');

    this.isOpen.set(true);
  }

  protected onInputChange(value: string): void {
    this.displayValue.set(value);
  }

  protected onInputBlur(value: string): void {
    if (!value) {
      const currentSelected = this.selectedDate();
      if (currentSelected !== null) {
        this.selectedDate.set(null);
        this.displayValue.set('');
        this.dateChange.emit('');
      }
      return;
    }

    const date = this._dateService.parseDate(value);
    if (date) {
      const currentSelected = this.selectedDate();
      const isDifferent = !currentSelected || !this._dateService.isSameDay(date, currentSelected);

      this.selectedDate.set(date);
      this.currentDate.set(new Date(date));
      this.displayValue.set(this._dateService.formatDate(date));

      if (isDifferent) {
        this.dateChange.emit(date.toISOString());
      }
    } else {
      // Invalid date, revert to last valid date
      const selected = this.selectedDate();
      this.displayValue.set(selected ? this._dateService.formatDate(selected) : '');
    }
  }

  protected onInputKeydown(event: KeyboardEvent): void {
    if (event.key !== 'ArrowDown' || !this.isOpen()) return;
    event.preventDefault();
    queueMicrotask(() => this._calendar()?.focusActiveDay());
  }

  protected onDateSelect(date: Date | null): void {
    const currentSelected = this.selectedDate();
    const isDifferent =
      !currentSelected || (date && !this._dateService.isSameDay(date, currentSelected));

    this.selectedDate.set(date);
    this.displayValue.set(date ? this._dateService.formatDate(date) : '');

    if (isDifferent) {
      this.dateChange.emit(date ? date.toISOString() : '');
    }

    this.isOpen.set(false);
    this.focusDateInput();
  }

  protected onClear(): void {
    this.selectedDate.set(null);
    this.displayValue.set('');
    this.dateChange.emit('');
    this.isOpen.set(false);
  }

  protected onClose(): void {
    this.isOpen.set(false);
    if (this.openMode() === 'mobile') this.focusDateInput();
  }

  protected onCurrentDateChange(date: Date): void {
    this.currentDate.set(date);
  }

  private focusDateInput(): void {
    const input = this._dateInput()?.inputElement()?.nativeElement;
    if (!input) return;

    this._isRestoringFocus = true;
    try {
      input.focus();
    } finally {
      this._isRestoringFocus = false;
    }
  }
}
