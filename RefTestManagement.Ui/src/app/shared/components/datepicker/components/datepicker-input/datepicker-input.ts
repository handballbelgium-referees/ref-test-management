import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  output,
  viewChild,
} from '@angular/core';

@Component({
  selector: 'app-datepicker-input',
  templateUrl: './datepicker-input.html',
  styleUrl: './datepicker-input.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class DatepickerInput {
  readonly value = input<string>('');
  readonly placeholder = input<string>('dd/mm/yyyy');
  readonly isOpen = input<boolean>(false);
  readonly readonly = input<boolean>(false);

  readonly focus = output<void>();
  readonly inputChange = output<string>();
  readonly blur = output<string>();

  readonly inputElement = viewChild<ElementRef>('input');

  protected onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.inputChange.emit(input.value);
  }

  protected onBlur(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.blur.emit(input.value);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (['Backspace', 'Delete', 'Tab', 'Escape', 'Enter'].includes(event.key)) {
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
