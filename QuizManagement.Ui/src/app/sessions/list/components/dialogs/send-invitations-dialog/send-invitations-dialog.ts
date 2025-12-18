import { ChangeDetectionStrategy, Component, computed, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface IInvitationSummary {
  newInvitations: Array<{ name: string; email: string }>;
  resendInvitations: Array<{ name: string; email: string }>;
}

@Component({
  selector: 'app-send-invitations-dialog',
  imports: [TranslatePipe],
  templateUrl: './send-invitations-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'host',
  },
})
export class SendInvitationsDialog {
  readonly show = input.required<boolean>();
  readonly summary = input.required<IInvitationSummary>();

  readonly confirm = output<void>();
  readonly cancel = output<void>();

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
