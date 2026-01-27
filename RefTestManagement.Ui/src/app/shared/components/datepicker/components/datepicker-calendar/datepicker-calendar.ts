import {
  ChangeDetectionStrategy,
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
import { ViewMode } from '../../models/datepicker.types';
import { Datepicker } from '../../services/datepicker';
import { CalendarFooter } from './components/calendar-footer/calendar-footer';
import { CalendarHeader } from './components/calendar-header/calendar-header';
import { CalendarView } from './components/calendar-view/calendar-view';

@Component({
  selector: 'app-datepicker-calendar',
  imports: [CalendarHeader, CalendarView, CalendarFooter],
  templateUrl: './datepicker-calendar.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class DatepickerCalendar {
  private readonly _dateService = inject(Datepicker);
  private readonly _elRef = inject(ElementRef);
  private readonly _destroyRef = inject(DestroyRef);
  private _clickListener?: (event: MouseEvent) => void;
  private _viewportResizeListener?: () => void;

  readonly selectedDate = input<Date | null>(null);
  readonly currentDate = input.required<Date>();
  readonly inputElement = input<ElementRef | undefined>(undefined);
  readonly openMode = input<'mobile' | 'desktop'>();

  protected readonly dateSelect = output<Date | null>();
  protected readonly close = output<void>();
  protected readonly currentDateChange = output<Date>();

  protected readonly calendar = viewChild<ElementRef>('calendar');

  protected readonly viewMode = signal<ViewMode>('days');

  protected readonly viewTitle = computed(() => {
    return this._dateService.getViewTitle(this.currentDate(), this.viewMode());
  });

  protected readonly isPositioned = signal(false);

  constructor() {
    // Position calendar when opened
    effect(() => {
      if (this.openMode() === 'desktop') {
        this.isPositioned.set(false);

        requestAnimationFrame(() => {
          this.positionCalendar();
          this.isPositioned.set(true);
        });
      }
    });

    // Click listener for desktop
    setTimeout(() => {
      if (this.openMode() === 'desktop') {
        this._clickListener = (event: MouseEvent) => {
          const target = event.target as Node;
          const calendar = this.calendar();
          const inputEl = this.inputElement();

          if (
            this._elRef.nativeElement.contains(target) ||
            (calendar && calendar.nativeElement.contains(target)) ||
            (inputEl && inputEl.nativeElement.contains(target))
          ) {
            return;
          }
          this.close.emit();
        };
        if (this.openMode() === 'desktop') {
          document.addEventListener('click', this._clickListener);
        }
      }

      this._destroyRef.onDestroy(() => {
        if (this._clickListener) {
          document.removeEventListener('click', this._clickListener);
        }
      });
    }, 300);

    // Viewport resize listener
    const viewport = window.visualViewport;
    if (viewport) {
      this._viewportResizeListener = () => {
        requestAnimationFrame(() => this.positionCalendar());
      };
      viewport.addEventListener('resize', this._viewportResizeListener);

      this._destroyRef.onDestroy(() => {
        if (this._viewportResizeListener) {
          viewport.removeEventListener('resize', this._viewportResizeListener);
        }
      });
    }
  }

  protected toggleView(): void {
    const mode = this.viewMode();
    if (mode === 'days') {
      this.viewMode.set('months');
    } else if (mode === 'months') {
      this.viewMode.set('years');
    }
  }

  protected onPrevious(): void {
    const mode = this.viewMode();
    const newDate = new Date(this.currentDate());

    if (mode === 'days') {
      newDate.setMonth(newDate.getMonth() - 1);
    } else if (mode === 'months') {
      newDate.setFullYear(newDate.getFullYear() - 1);
    } else {
      newDate.setFullYear(newDate.getFullYear() - 10);
    }

    this.currentDateChange.emit(newDate);
  }

  protected onNext(): void {
    const mode = this.viewMode();
    const newDate = new Date(this.currentDate());

    if (mode === 'days') {
      newDate.setMonth(newDate.getMonth() + 1);
    } else if (mode === 'months') {
      newDate.setFullYear(newDate.getFullYear() + 1);
    } else {
      newDate.setFullYear(newDate.getFullYear() + 10);
    }

    this.currentDateChange.emit(newDate);
  }

  protected onDateSelect(date: Date): void {
    this.dateSelect.emit(date);
    this.viewMode.set('days');
  }

  protected onMonthSelect(monthIndex: number): void {
    const newDate = new Date(this.currentDate());
    newDate.setMonth(monthIndex);
    this.currentDateChange.emit(newDate);
    this.viewMode.set('days');
  }

  protected onYearSelect(year: number): void {
    const newDate = new Date(this.currentDate());
    newDate.setFullYear(year);
    this.currentDateChange.emit(newDate);
    this.viewMode.set('months');
  }

  protected onToday(): void {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    this.dateSelect.emit(today);
  }

  protected onClear(): void {
    this.dateSelect.emit(null);
    this.close.emit();
  }

  private positionCalendar(): void {
    if (this.openMode() !== 'desktop') return;

    const inputEl = this.inputElement()?.nativeElement;
    const calendarEl = this.calendar()?.nativeElement;
    if (!inputEl || !calendarEl) return;

    // temporarily hide for accurate measurement
    calendarEl.style.visibility = 'hidden';
    calendarEl.style.display = 'block';

    requestAnimationFrame(() => {
      const inputRect = inputEl.getBoundingClientRect();
      const calendarRect = calendarEl.getBoundingClientRect();
      const vv = window.visualViewport;
      const offsetTop = vv?.offsetTop ?? 0;
      const offsetLeft = vv?.offsetLeft ?? 0;
      const viewportHeight = vv?.height ?? window.innerHeight;
      const viewportWidth = vv?.width ?? window.innerWidth;
      const margin = 8;

      const spaceBelow = viewportHeight + offsetTop - inputRect.bottom - margin;
      const spaceAbove = inputRect.top + offsetTop - margin;

      let top: number;
      if (calendarRect.height <= spaceBelow) {
        top = inputRect.bottom + offsetTop + margin;
      } else if (calendarRect.height <= spaceAbove) {
        top = inputRect.top + offsetTop - calendarRect.height - margin;
      } else {
        top = Math.max(margin, offsetTop + margin);
        calendarEl.style.maxHeight = `${viewportHeight - 2 * margin}px`;
        calendarEl.style.overflowY = 'auto';
      }

      let left = inputRect.left + offsetLeft;
      left = Math.max(
        margin,
        Math.min(left, viewportWidth + offsetLeft - calendarRect.width - margin),
      );

      calendarEl.style.top = `${top}px`;
      calendarEl.style.left = `${left}px`;
      calendarEl.style.right = 'auto';
      calendarEl.style.visibility = 'visible';
    });
  }
}
