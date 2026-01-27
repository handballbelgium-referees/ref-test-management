import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
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
  readonly invitationSent = input.required<boolean>();
  readonly resultsSent = input.required<boolean>();
  readonly sendInvitationsAutomatically = input.required<boolean>();
  readonly sendResultsAutomatically = input.required<boolean>();
  readonly status = input.required<RefTestStatus>();
  protected readonly edit = output<void>();

  protected readonly RefTestStatus = RefTestStatus;

  protected readonly canEditNotificationSettings = computed(() => {
    const status = this.status();
    const invitationSent = this.invitationSent();
    const resultsSent = this.resultsSent();

    // Can update sendInvitationsAutomatically if pending and invitation not yet sent
    const canEditInvitations = status === RefTestStatus.Pending && !invitationSent;

    // Can update sendResultsAutomatically if not expired and results not yet sent
    const canEditResults = status !== RefTestStatus.Expired && !resultsSent;

    return canEditInvitations || canEditResults;
  });
}
