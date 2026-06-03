import { Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestStatus } from '../../../../../../../../graphql/generated';
import { HasPermission } from '../../../../../../auth/directives/has-permission.directive';
import { Permissions } from '../../../../../../auth/models/permissions';
import { LocalizedDate } from './../../../../../../shared/pipes/localized-date';

@Component({
  selector: 'app-timeline-card',
  imports: [TranslatePipe, LocalizedDate, HasPermission],
  templateUrl: './timeline-card.html',
  host: { class: 'block' },
})
export class TimelineCard {
  readonly scheduledAt = input<string | undefined>(undefined);
  readonly startedAt = input<string | undefined>(undefined);
  readonly completedAt = input<string | undefined>(undefined);
  readonly status = input.required<RefTestStatus>();
  protected readonly Permissions = Permissions;

  protected readonly extendTime = output<void>();
}
