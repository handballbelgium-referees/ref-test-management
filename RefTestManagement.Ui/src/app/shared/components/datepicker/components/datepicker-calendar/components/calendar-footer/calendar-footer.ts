import { Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-calendar-footer',
  imports: [TranslatePipe],
  templateUrl: './calendar-footer.html',
  host: {
    class: 'bock',
  },
})
export class CalendarFooter {
  readonly isSmallTouchDevice = input<boolean>(false);

  protected readonly today = output<void>();
  protected readonly clear = output<void>();
}
