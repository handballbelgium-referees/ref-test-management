import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Datepicker } from '../datepicker/datepicker';
import { TimePicker } from './components/time-picker/time-picker';

/**
 * Datetime picker that combines the existing Datepicker (date part)
 * with a TimePicker dropdown (time part).
 *
 * Emits an ISO-8601 string ("YYYY-MM-DDTHH:mm") whenever either part changes,
 * or an empty string when the date is cleared.
 *
 * Usage:
 *   <app-datetime-picker [value]="isoString" (valueChange)="onDateTimeChange($event)" />
 */
@Component({
  selector: 'app-datetime-picker',
  imports: [Datepicker, TimePicker],
  templateUrl: './datetime-picker.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DatetimePicker {
  /** Current "YYYY-MM-DDTHH:mm" string, or empty. */
  readonly value = input<string>('');

  readonly valueChange = output<string>();

  /** The date portion as "YYYY-MM-DD" derived from the input value. */
  protected readonly dateValue = computed(() => {
    const v = this.value();
    return v ? v.split('T')[0] : '';
  });

  /** "HH:mm" string for the TimePicker, derived from the input value. */
  protected readonly timeValue = computed(() => {
    const v = this.value();
    if (!v || !v.includes('T')) return '00:00';
    const [hh, mm] = (v.split('T')[1] ?? '00:00').split(':');
    return `${(hh ?? '00').padStart(2, '0')}:${(mm ?? '00').padStart(2, '0')}`;
  });

  /**
   * Called when the Datepicker emits a value (full ISO string or empty).
   * Extracts the local YYYY-MM-DD to avoid UTC offset surprises.
   */
  protected onDateChange(isoString: string): void {
    if (!isoString) {
      this.valueChange.emit('');
      return;
    }
    const d = new Date(isoString);
    const localDate = [
      d.getFullYear(),
      String(d.getMonth() + 1).padStart(2, '0'),
      String(d.getDate()).padStart(2, '0'),
    ].join('-');
    this.valueChange.emit(`${localDate}T${this.timeValue()}`);
  }

  /** Called when the TimePicker emits an updated "HH:mm" value. */
  protected onTimeChange(time: string): void {
    const date = this.dateValue();
    if (!date) return;
    this.valueChange.emit(`${date}T${time}`);
  }

  /** Called when the TimePicker clear button is pressed — clears the entire datetime value. */
  protected onTimeClear(): void {
    this.valueChange.emit('');
  }
}
