import {
  Component,
  ElementRef,
  input,
  output,
  viewChild,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-datepicker-input',
  imports: [TranslatePipe],
  templateUrl: './datepicker-input.html',
  styleUrl: './datepicker-input.css',
  host: {
    class: 'block',
  },
})
export class DatepickerInput {
  readonly value = input<string>('');
  readonly placeholder = input<string>('dd/mm/yyyy');
  readonly label = input<string>('');
  readonly calendarId = input.required<string>();
  readonly isOpen = input<boolean>(false);
  readonly readonly = input<boolean>(false);

  protected readonly focus = output<void>();
  protected readonly inputChange = output<string>();
  protected readonly blur = output<string>();
  protected readonly keydown = output<KeyboardEvent>();

  readonly inputElement = viewChild<ElementRef<HTMLInputElement>>('input');

  protected onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.inputChange.emit(input.value);
  }

  protected onBlur(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.blur.emit(input.value);
  }

  protected onKeydown(event: KeyboardEvent): void {
    this.keydown.emit(event);

    if (
      ['Backspace', 'Delete', 'Tab', 'Escape', 'Enter', 'ArrowUp', 'ArrowDown'].includes(
        event.key,
      )
    ) {
      return;
    }

    if (event.ctrlKey && ['a', 'c', 'v', 'x'].includes(event.key.toLowerCase())) {
      return;
    }

    if (['Home', 'End', 'ArrowLeft', 'ArrowRight'].includes(event.key)) {
      return;
    }

    if (!/^[0-9/]$/.test(event.key)) {
      event.preventDefault();
    }
  }
}
