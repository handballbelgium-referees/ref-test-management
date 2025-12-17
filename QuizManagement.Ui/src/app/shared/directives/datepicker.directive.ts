import {
  AfterViewInit,
  DestroyRef,
  Directive,
  ElementRef,
  inject,
  input,
  output,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';
import { Datepicker, DatepickerOptions } from 'flowbite';

@Directive({
  selector: '[appDatepicker]',
})
export class DatepickerDirective implements AfterViewInit {
  private readonly _el = inject(ElementRef);
  private readonly _translate = inject(TranslateService);
  private readonly _destroyRef = inject(DestroyRef);
  private _datepicker?: Datepicker;

  readonly value = input<string>();
  readonly dateChange = output<string>();

  ngAfterViewInit(): void {
    this.initDatepicker();

    this._translate.onLangChange.pipe(takeUntilDestroyed(this._destroyRef)).subscribe(() => {
      const currentDate = this._datepicker?.getDate();
      this._datepicker?.destroy();
      this.initDatepicker();
      if (currentDate) {
        this._datepicker?.setDate(currentDate);
      }
    });

    this._destroyRef.onDestroy(() => this._datepicker?.destroy());
  }

  private initDatepicker(): void {
    // Allow only numbers and slashes for date input
    this._el.nativeElement.addEventListener('keydown', (e: KeyboardEvent) => {
      const allowedKeys = [
        'Backspace',
        'Delete',
        'Tab',
        'Escape',
        'Enter',
        'ArrowLeft',
        'ArrowRight',
        'ArrowUp',
        'ArrowDown',
      ];
      const isNumber = (e.key >= '0' && e.key <= '9') || e.key === '/';
      const isAllowedKey = allowedKeys.includes(e.key);
      const isCtrlCmd = e.ctrlKey || e.metaKey;

      if (!isNumber && !isAllowedKey && !isCtrlCmd) {
        e.preventDefault();
      }
    });

    const options: DatepickerOptions = {
      autohide: true,
      format: 'dd/mm/yyyy',
      buttons: true,
      autoSelectToday: 0,
      defaultDatepickerId: null,
      title: null,
      rangePicker: false,
    };

    this._datepicker = new Datepicker(this._el.nativeElement, options);

    if (this.value()) {
      // Parse yyyy-mm-dd to Date
      const [year, month, day] = this.value()!.split('-');
      const date = new Date(parseInt(year), parseInt(month) - 1, parseInt(day));
      this._datepicker.setDate(date);
    }

    this._el.nativeElement.addEventListener('changeDate', (e: any) => {
      if (e.detail.date) {
        const date = new Date(e.detail.date);
        // Set to start of day in local timezone, then convert to ISO string
        date.setHours(0, 0, 0, 0);
        this.dateChange.emit(date.toISOString());
      } else {
        this.dateChange.emit('');
      }
    });

    // Handle manual date input
    this._el.nativeElement.addEventListener('blur', () => {
      const inputValue = this._el.nativeElement.value;
      if (inputValue && inputValue.match(/^\d{2}\/\d{2}\/\d{4}$/)) {
        // Convert dd/mm/yyyy to ISO string
        const [day, month, year] = inputValue.split('/');
        const date = new Date(parseInt(year), parseInt(month) - 1, parseInt(day));

        // Validate date is valid
        if (!isNaN(date.getTime())) {
          date.setHours(0, 0, 0, 0);
          this.dateChange.emit(date.toISOString());
        }
      } else if (!inputValue) {
        this.dateChange.emit('');
      }
    });
  }
}
