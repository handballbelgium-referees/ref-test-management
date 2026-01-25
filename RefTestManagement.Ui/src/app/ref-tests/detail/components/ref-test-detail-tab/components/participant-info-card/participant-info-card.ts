import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestStatus } from '../../../../../../../../graphql/generated';

@Component({
  selector: 'app-participant-info-card',
  imports: [TranslatePipe],
  templateUrl: './participant-info-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class ParticipantInfoCard {
  name = input.required<string>();
  email = input.required<string>();
  status = input.required<RefTestStatus>();

  readonly edit = output<void>();

  protected readonly RefTestStatus = RefTestStatus;
}
