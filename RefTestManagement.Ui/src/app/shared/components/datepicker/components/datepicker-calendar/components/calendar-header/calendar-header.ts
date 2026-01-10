import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ViewMode } from '../../../../models/datepicker.types';

@Component({
  selector: 'app-calendar-header',
  imports: [TranslatePipe],
  templateUrl: './calendar-header.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'bock',
  },
})
export class CalendarHeader {
  readonly viewMode = input.required<ViewMode>();
  readonly viewTitle = input.required<string>();
  readonly isSmallTouchDevice = input<boolean>(false);

  readonly previous = output<void>();
  readonly next = output<void>();
  readonly toggleView = output<void>();
}
