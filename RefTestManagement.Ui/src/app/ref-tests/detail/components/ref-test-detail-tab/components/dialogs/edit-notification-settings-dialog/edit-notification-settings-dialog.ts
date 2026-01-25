import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { disabled, form, FormField } from '@angular/forms/signals';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, EMPTY, finalize, map, tap } from 'rxjs';
import {
  RefTestStatus,
  UpdateRefTestNotificationSettingsGQL,
} from '../../../../../../../../../graphql/generated';
import { Banner as BannerService } from '../../../../../../../services/banner';
import { Banner } from '../../../../../../../shared/components/banner/banner';
import { toSnakeCase } from '../../../../../../../shared/utils/string-utils';

interface INotificationSettings {
  sendInvitationsAutomatically: boolean;
  sendResultsAutomatically: boolean;
}

@Component({
  selector: 'app-edit-notification-settings-dialog',
  imports: [TranslatePipe, FormField, Banner],
  templateUrl: './edit-notification-settings-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditNotificationSettingsDialog {
  private readonly _updateRefTestNotificationSettingsGQL = inject(
    UpdateRefTestNotificationSettingsGQL,
  );
  private readonly _translateService = inject(TranslateService);
  private readonly _bannerServiceRoot = inject(BannerService);
  private readonly _destroyRef = inject(DestroyRef);

  // Create isolated banner manager for this dialog
  protected readonly bannerManager = this._bannerServiceRoot.createIsolated();

  protected readonly settingsModel = signal<INotificationSettings>({
    sendInvitationsAutomatically: false,
    sendResultsAutomatically: false,
  });

  protected readonly settingsForm = form(this.settingsModel, (schema) => {
    disabled(schema.sendInvitationsAutomatically, () => !this.canEditInvitations());
    disabled(schema.sendResultsAutomatically, () => !this.canEditResults());
  });

  protected readonly loading = signal(false);

  readonly show = input.required<boolean>();
  readonly refTestId = signal<string>('');
  readonly status = signal<RefTestStatus>(RefTestStatus.Pending);
  readonly invitationSent = signal<boolean>(false);
  readonly resultsSent = signal<boolean>(false);

  protected readonly canEditInvitations = computed(() => {
    // Can update sendInvitationsAutomatically only if pending and invitation not yet sent
    return this.status() === RefTestStatus.Pending && !this.invitationSent();
  });

  protected readonly canEditResults = computed(() => {
    // Can update sendResultsAutomatically only if not expired and results not yet sent
    return this.status() !== RefTestStatus.Expired && !this.resultsSent();
  });
  readonly closeDialog = output<void>();
  readonly cancel = output<void>();

  constructor() {
    effect(() => {
      if (this.show()) {
        document.body.style.overflow = 'hidden';
      } else {
        document.body.style.overflow = '';
      }
    });
  }

  initialize(
    refTestId: string,
    status: RefTestStatus,
    invitationSent: boolean,
    resultsSent: boolean,
    sendInvitationsAutomatically: boolean,
    sendResultsAutomatically: boolean,
  ): void {
    this.refTestId.set(refTestId);
    this.status.set(status);
    this.invitationSent.set(invitationSent);
    this.resultsSent.set(resultsSent);
    this.settingsModel.set({
      sendInvitationsAutomatically,
      sendResultsAutomatically,
    });
  }

  protected onSave(): void {
    const data = this.settingsModel();

    this._updateRefTestNotificationSettingsGQL
      .mutate({
        variables: {
          input: {
            id: this.refTestId(),
            sendInvitationsAutomatically: data.sendInvitationsAutomatically,
            sendResultsAutomatically: data.sendResultsAutomatically,
          },
        },
      })
      .pipe(
        tap((result) => this.loading.set(result.loading ?? false)),
        map((result) => result.data?.updateRefTestNotificationSettings),
        tap((data) => {
          if (data?.errors && data.errors.length > 0) {
            const error = data.errors[0];
            if ('__typename' in error && error.__typename) {
              this.bannerManager.error(
                this._translateService.instant(toSnakeCase(error.__typename)),
              );
            }
            return;
          }
          if (data?.refTest) {
            this.bannerManager.success(
              this._translateService.instant('ref_tests.detail.edit_notification_settings.success'),
            );
            this.closeDialog.emit();
          }
        }),
        catchError(() => {
          this.bannerManager.error(
            this._translateService.instant('ref_tests.detail.edit_notification_settings.error'),
          );
          return EMPTY;
        }),
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  protected onCancel(): void {
    this.cancel.emit();
  }

  protected onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.cancel.emit();
    }
  }
}
