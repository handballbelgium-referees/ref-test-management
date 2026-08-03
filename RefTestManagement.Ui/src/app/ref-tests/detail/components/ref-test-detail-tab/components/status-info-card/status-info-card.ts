import { Component, computed, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestStatus } from '../../../../../../../../graphql/generated';
import { HasPermission } from '../../../../../../auth/directives/has-permission.directive';
import { Permissions } from '../../../../../../auth/models/permissions';

@Component({
  selector: 'app-status-info-card',
  imports: [TranslatePipe, HasPermission],
  templateUrl: './status-info-card.html',
  host: { class: 'block' },
})
export class StatusInfoCard {
  readonly invitationSent = input.required<boolean>();
  readonly resultsSent = input.required<boolean>();
  readonly sendInvitationsAutomatically = input.required<boolean>();
  readonly sendResultsAutomatically = input.required<boolean>();
  readonly status = input.required<RefTestStatus>();
  readonly isAnonymized = input<boolean>(false);
  protected readonly Permissions = Permissions;
  protected readonly edit = output<void>();

  protected readonly canEditNotificationSettings = computed(() => {
    if (this.isAnonymized()) return false;

    const status = this.status();
    const invitationSent = this.invitationSent();
    const resultsSent = this.resultsSent();

    // Can update sendInvitationsAutomatically if pending and invitation not yet sent
    const canEditInvitations = status === 'PENDING' && !invitationSent;

    // Can update sendResultsAutomatically if not expired and results not yet sent
    const canEditResults = status !== 'EXPIRED' && !resultsSent;

    return canEditInvitations || canEditResults;
  });
}
