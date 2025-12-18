import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-calendar-footer',
  imports: [TranslatePipe],
  templateUrl: './calendar-footer.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'bock',
  },
})
export class CalendarFooter {
  readonly isSmallTouchDevice = input<boolean>(false);

  readonly today = output<void>();
  readonly clear = output<void>();
}
