import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { LocalizedDate } from './../../../../../../shared/pipes/localized-date';

@Component({
  selector: 'app-timeline-card',
  imports: [TranslatePipe, LocalizedDate],
  templateUrl: './timeline-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class TimelineCard {
  startedAt = input<string | undefined>(undefined);
  completedAt = input<string | undefined>(undefined);
}
