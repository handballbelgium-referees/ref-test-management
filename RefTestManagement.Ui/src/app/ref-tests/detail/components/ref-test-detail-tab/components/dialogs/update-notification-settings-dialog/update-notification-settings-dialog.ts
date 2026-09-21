import {
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { disabled, form, FormField, required } from '@angular/forms/signals';
import { TranslatePipe } from '@ngx-translate/core';
import {
  GetRefTestByIdQuery,
  RefTestStatus,
  UpdateRefTestNotificationSettingsInput,
} from '../../../../../../../../../graphql/generated';
import { IsolatedBannerManager } from '../../../../../../../services/banner';
import { Banner } from '../../../../../../../shared/components/banner/banner';
import { Dialog } from '../../../../../../../shared/components/dialog/dialog';

type RefTest = Extract<GetRefTestByIdQuery['refTest'], { __typename: 'RefTest' }>;

interface INotificationSettings {
  id: string;
  sendInvitationsAutomatically: boolean;
  sendResultsAutomatically: boolean;
  status: RefTestStatus;
  invitationSent: boolean;
  resultsSent: boolean;
}

@Component({
  selector: 'app-update-notification-settings-dialog',
  imports: [TranslatePipe, FormField, Banner, Dialog],
  templateUrl: './update-notification-settings-dialog.html',
})
export class UpdateNotificationSettingsDialog {
  protected readonly settingsModel = signal<INotificationSettings>({
    id: '',
    sendInvitationsAutomatically: false,
    sendResultsAutomatically: false,
    status: 'PENDING',
    invitationSent: false,
    resultsSent: false,
  });

  protected readonly settingsForm = form(this.settingsModel, (schema) => {
    required(schema.id);
    disabled(schema.sendInvitationsAutomatically, () => !this.canEditInvitations());
    disabled(schema.sendResultsAutomatically, () => !this.canEditResults());
  });

  readonly loading = input.required<boolean>();
  readonly show = input.required<boolean>();
  readonly initialData = input.required<RefTest | undefined>();
  readonly bannerManager = input.required<IsolatedBannerManager>();
  protected readonly confirm = output<UpdateRefTestNotificationSettingsInput>();
  protected readonly cancel = output<void>();

  protected readonly canEditInvitations = computed(() => {
    // Can update sendInvitationsAutomatically only if pending and invitation not yet sent
    return this.settingsModel().status === 'PENDING' && !this.settingsModel().invitationSent;
  });

  protected readonly canEditResults = computed(() => {
    // Can update sendResultsAutomatically only if not expired and results not yet sent
    return this.settingsModel().status !== 'EXPIRED' && !this.settingsModel().resultsSent;
  });

  constructor() {
    effect(() => {
      if (this.show()) {
        document.body.style.overflow = 'hidden';
      } else {
        document.body.style.overflow = '';
      }
    });

    effect(() => {
      const data = this.initialData();
      if (!data) return;
      this.settingsModel.set({
        id: data.id,
        sendInvitationsAutomatically: data.sendInvitationsAutomatically,
        sendResultsAutomatically: data.sendResultsAutomatically,
        status: data.status,
        invitationSent: data.invitationSent,
        resultsSent: data.resultsSent,
      });
    });
  }

  protected onSave(): void {
    if (this.settingsForm().invalid()) {
      this.settingsForm().markAsTouched();
      return;
    }

    const data = this.settingsModel();

    this.confirm.emit({
      id: data.id,
      sendInvitationsAutomatically: data.sendInvitationsAutomatically,
      sendResultsAutomatically: data.sendResultsAutomatically,
    });
  }

  protected onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.cancel.emit();
    }
  }
}
