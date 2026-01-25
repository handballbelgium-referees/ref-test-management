import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestStatus } from '../../../../../../../../graphql/generated';

@Component({
  selector: 'app-status-info-card',
  imports: [TranslatePipe],
  templateUrl: './status-info-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class StatusInfoCard {
  invitationSent = input.required<boolean>();
  resultsSent = input.required<boolean>();
  sendInvitationsAutomatically = input.required<boolean>();
  sendResultsAutomatically = input.required<boolean>();
  status = input.required<RefTestStatus>();

  protected readonly RefTestStatus = RefTestStatus;
}
