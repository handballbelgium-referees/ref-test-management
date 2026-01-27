import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { disabled, form, FormField, required } from '@angular/forms/signals';
import { TranslatePipe } from '@ngx-translate/core';
import {
  RefTest,
  RefTestStatus,
  UpdateRefTestNotificationSettingsInput,
} from '../../../../../../../../../graphql/generated';
import {
  Banner as BannerService,
  IsolatedBannerManager,
} from '../../../../../../../services/banner';
import { Banner } from '../../../../../../../shared/components/banner/banner';

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
  imports: [TranslatePipe, FormField, Banner],
  templateUrl: './update-notification-settings-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UpdateNotificationSettingsDialog {
  // Create isolated banner manager for this dialog
  protected readonly bannerManager = inject(BannerService).createIsolated();

  protected readonly settingsModel = signal<INotificationSettings>({
    id: '',
    sendInvitationsAutomatically: false,
    sendResultsAutomatically: false,
    status: RefTestStatus.Pending,
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
  protected readonly confirm = output<{
    input: UpdateRefTestNotificationSettingsInput;
    bannerManager: IsolatedBannerManager;
  }>();
  protected readonly cancel = output<void>();

  protected readonly canEditInvitations = computed(() => {
    // Can update sendInvitationsAutomatically only if pending and invitation not yet sent
    return (
      this.settingsModel().status === RefTestStatus.Pending && !this.settingsModel().invitationSent
    );
  });

  protected readonly canEditResults = computed(() => {
    // Can update sendResultsAutomatically only if not expired and results not yet sent
    return (
      this.settingsModel().status !== RefTestStatus.Expired && !this.settingsModel().resultsSent
    );
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
      input: {
        id: data.id,
        sendInvitationsAutomatically: data.sendInvitationsAutomatically,
        sendResultsAutomatically: data.sendResultsAutomatically,
      },
      bannerManager: this.bannerManager,
    });
  }

  protected onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.cancel.emit();
    }
  }
}
