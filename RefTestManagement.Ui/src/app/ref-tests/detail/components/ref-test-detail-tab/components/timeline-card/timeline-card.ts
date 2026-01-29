import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestStatus } from '../../../../../../../../graphql/generated';
import { LocalizedDate } from './../../../../../../shared/pipes/localized-date';

@Component({
  selector: 'app-timeline-card',
  imports: [TranslatePipe, LocalizedDate],
  templateUrl: './timeline-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class TimelineCard {
  readonly startedAt = input<string | undefined>(undefined);
  readonly completedAt = input<string | undefined>(undefined);
  readonly status = input.required<RefTestStatus>();

  protected readonly extendTime = output<void>();

  readonly RefTestStatus = RefTestStatus;
}
