import { Component, computed, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { IsolatedBannerManager } from '../../../../../services/banner';
import { Banner } from '../../../../../shared/components/banner/banner';

interface IInvitationSummary {
  newInvitations: Array<{ name: string; email: string }>;
  resendInvitations: Array<{ name: string; email: string }>;
}

@Component({
  selector: 'app-send-invitations-dialog',
  imports: [TranslatePipe, Banner],
  templateUrl: './send-invitations-dialog.html',
  host: {
    class: 'host',
  },
})
export class SendInvitationsDialog {
  readonly loading = input.required<boolean>();
  readonly show = input.required<boolean>();
  readonly summary = input.required<IInvitationSummary>();
  readonly bannerManager = input.required<IsolatedBannerManager>();

  protected readonly confirm = output<void>();
  protected readonly cancel = output<void>();
  protected readonly totalCount = computed(() => {
    const summary = this.summary();
    return summary.newInvitations.length + summary.resendInvitations.length;
  });

  constructor() {
    effect(() => {
      if (this.show()) {
        document.body.style.overflow = 'hidden';
      } else {
        document.body.style.overflow = '';
      }
    });
  }
}
