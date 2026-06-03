import { NgTemplateOutlet } from '@angular/common';
import {
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { Datepicker as DatepickerService } from '../../../datepicker/services/datepicker';

@Component({
  selector: 'app-time-picker',
  imports: [NgTemplateOutlet],
  templateUrl: './time-picker.html',
  providers: [DatepickerService],
  host: {
    class: 'relative block',
    '(document:keydown.escape)': 'close()',
  },
})
export class TimePicker {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _host = inject(ElementRef<HTMLElement>);
  private readonly _dateService = inject(DatepickerService);
  private _resizeListener?: () => void;

  /** "HH:mm" string */
  readonly value = input<string>('00:00');
  readonly valueChange = output<string>();
  readonly clear = output<void>();

  protected readonly isSmallTouchDevice = signal(this._dateService.detectSmallTouchDevice());
  protected readonly isOpen = signal(false);
  protected readonly openMode = signal<'mobile' | 'desktop'>('desktop');
  protected readonly view = signal<'hours' | 'minutes'>('hours');

  // ── Clock geometry (px) ────────────────────────────────────────────────
  protected readonly CLOCK = 224; // svg/container side length
  private readonly CX = 112;
  private readonly CY = 112;
  private readonly OUTER_R = 84; // 1-12 and minute-step ring radius
  private readonly INNER_R = 52; // 0, 13-23 ring radius
  private readonly BTN_R = 14; // half of 28 px button

  protected readonly outerHours = [12, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11] as const;
  protected readonly innerHours = [0, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23] as const;
  protected readonly minuteSteps = [0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55] as const;

  /** Small tick dots at the 12 clock positions (pre-computed, never change). */
  protected readonly ticks: ReadonlyArray<{ cx: number; cy: number }>;

  // ── Derived display values ───────────────────────────────────────────────
  protected readonly hourValue = computed(() => {
    const [hh] = (this.value() || '00:00').split(':');
    return Math.min(23, Math.max(0, parseInt(hh ?? '0', 10)));
  });

  protected readonly minuteValue = computed(() => {
    const parts = (this.value() || '00:00').split(':');
    return Math.min(59, Math.max(0, parseInt(parts[1] ?? '0', 10)));
  });

  protected readonly displayValue = computed(
    () =>
      `${String(this.hourValue()).padStart(2, '0')}:${String(this.minuteValue()).padStart(2, '0')}`,
  );

  // ── SVG hand endpoints ───────────────────────────────────────────────────
  private readonly _hourIsInner = computed(() => {
    const h = this.hourValue();
    return h === 0 || h >= 13;
  });

  protected readonly hourHandEnd = computed(() => {
    const h = this.hourValue();
    const idx = h === 0 ? 0 : h <= 12 ? h % 12 : h - 12;
    const r = this._hourIsInner() ? this.INNER_R : this.OUTER_R;
    return this._pt(this._angle(idx, 12), r);
  });

  protected readonly minuteHandEnd = computed(() =>
    this._pt(this._angle(this.minuteValue(), 60), this.OUTER_R),
  );

  constructor() {
    const tickR = this.CX - 8;
    this.ticks = Array.from({ length: 12 }, (_, i) => {
      const a = this._angle(i, 12);
      return { cx: this.CX + tickR * Math.cos(a), cy: this.CY + tickR * Math.sin(a) };
    });

    const onDocClick = (e: MouseEvent) => {
      if (
        this.isOpen() &&
        this.openMode() === 'desktop' &&
        !this._host.nativeElement.contains(e.target as Node)
      ) {
        this.isOpen.set(false);
      }
    };
    document.addEventListener('click', onDocClick, true);

    this._resizeListener = () => {
      const isMobile = this._dateService.detectSmallTouchDevice();
      this.isSmallTouchDevice.set(isMobile);
      if (this.isOpen()) {
        this.openMode.set(isMobile ? 'mobile' : 'desktop');
      }
    };
    window.addEventListener('resize', this._resizeListener);

    this._destroyRef.onDestroy(() => {
      document.removeEventListener('click', onDocClick, true);
      if (this._resizeListener) window.removeEventListener('resize', this._resizeListener);
    });
  }

  protected open(): void {
    this.openMode.set(this._dateService.detectSmallTouchDevice() ? 'mobile' : 'desktop');
    this.view.set('hours');
    this.isOpen.set(true);
  }

  protected close(): void {
    this.isOpen.set(false);
  }

  protected selectHour(h: number): void {
    this.emit(h, this.minuteValue());
    this.view.set('minutes');
  }

  protected selectMinuteStep(m: number): void {
    this.emit(this.hourValue(), m);
    this.close();
  }

  protected onClear(): void {
    this.clear.emit();
    this.close();
  }

  protected nudgeMinute(delta: number): void {
    this.emit(this.hourValue(), Math.min(59, Math.max(0, this.minuteValue() + delta)));
  }

  /** Block non-time characters; allow only digits, colon, and control keys. */
  protected onKeydown(event: KeyboardEvent): void {
    if (
      [
        'Backspace',
        'Delete',
        'Tab',
        'Escape',
        'Enter',
        'ArrowLeft',
        'ArrowRight',
        'Home',
        'End',
      ].includes(event.key)
    )
      return;
    if (event.ctrlKey && ['a', 'c', 'v', 'x'].includes(event.key.toLowerCase())) return;
    if (!/^[0-9:]$/.test(event.key)) event.preventDefault();
  }

  /** Parse a typed "HH:mm" or "HHmm" string on blur and emit if valid. */
  protected onBlur(event: Event): void {
    const raw = (event.target as HTMLInputElement).value.trim();
    // Accept "HH:mm" or "HHmm"
    const match = /^(\d{1,2}):?(\d{2})$/.exec(raw);
    if (!match) return;
    const h = Math.min(23, Math.max(0, parseInt(match[1], 10)));
    const m = Math.min(59, Math.max(0, parseInt(match[2], 10)));
    this.emit(h, m);
    // Reset the input to the canonical formatted value
    (event.target as HTMLInputElement).value = `${this.pad(h)}:${this.pad(m)}`;
  }

  protected pad(n: number): string {
    return String(n).padStart(2, '0');
  }

  /**
   * CSS `left`/`top` position for a clock button at ring-index `i`
   * (0 = 12 o'clock, clockwise) out of `total` positions at radius `r`.
   */
  protected itemStyle(i: number, total: number, r: number): Record<string, string> {
    const a = this._angle(i, total);
    return {
      left: `${this.CX + r * Math.cos(a) - this.BTN_R}px`,
      top: `${this.CY + r * Math.sin(a) - this.BTN_R}px`,
    };
  }

  private emit(h: number, m: number): void {
    this.valueChange.emit(`${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`);
  }

  /** Angle in radians: index 0 points up (12 o'clock), clockwise. */
  private _angle(index: number, total: number): number {
    return (index / total) * 2 * Math.PI - Math.PI / 2;
  }

  private _pt(angle: number, r: number): { x: number; y: number } {
    return { x: this.CX + r * Math.cos(angle), y: this.CY + r * Math.sin(angle) };
  }
}
