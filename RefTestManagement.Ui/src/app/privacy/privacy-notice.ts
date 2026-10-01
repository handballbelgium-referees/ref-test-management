import { DOCUMENT } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, EMPTY, finalize, tap } from 'rxjs';
import {
  ConfirmPersonalDataExportGQL,
  GetPrivacyNoticeGQL,
  RequestPersonalDataExportGQL,
} from '../../../graphql/generated';
import { LocalizedDate } from '../shared/pipes/localized-date';
import { LanguageConfig } from '../services/language-config';

@Component({
  selector: 'app-privacy-notice',
  imports: [ReactiveFormsModule, TranslatePipe],
  providers: [LocalizedDate],
  templateUrl: './privacy-notice.html',
  host: {
    class: 'block',
  },
})
export class PrivacyNotice {
  private readonly _document = inject(DOCUMENT);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _languageConfig = inject(LanguageConfig);
  private readonly _requestPersonalDataExportGQL = inject(RequestPersonalDataExportGQL);
  private readonly _confirmPersonalDataExportGQL = inject(ConfirmPersonalDataExportGQL);
  private readonly _confirmationKey = signal(this.readAndRemoveConfirmationKey());
  private readonly _getPrivacyNoticeGQL = inject(GetPrivacyNoticeGQL);
  private readonly _localizedDate = inject(LocalizedDate);

  readonly requestForm = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
  });
  readonly requestAttempted = signal(false);
  readonly requestPending = signal(false);
  readonly requestAcknowledged = signal(false);
  readonly requestError = signal(false);
  readonly confirmationPending = signal(false);
  readonly confirmationError = signal(false);
  readonly confirmationSucceeded = signal(false);
  readonly hasConfirmation = computed(
    () => this._confirmationKey() !== null || this.confirmationSucceeded(),
  );

  private readonly _privacyNoticeResult = toSignal(this._getPrivacyNoticeGQL.watch().valueChanges, {
    initialValue: null,
  });

  readonly privacyNotice = computed(() => this._privacyNoticeResult()?.data?.privacyNotice ?? null);

  readonly effectiveDate = computed(() => {
    const effectiveDate = this.privacyNotice()?.noticeEffectiveDate;
    return this._localizedDate.transform(effectiveDate, 'longDate') ?? effectiveDate ?? '';
  });

  requestPersonalDataExport(): void {
    if (this.requestPending()) return;

    this.requestAttempted.set(true);
    this.requestAcknowledged.set(false);
    this.requestError.set(false);

    if (this.requestForm.invalid) {
      this.requestForm.markAllAsTouched();
      return;
    }

    this._requestPersonalDataExportGQL
      .mutate({
        variables: {
          input: {
            input: {
              email: this.requestForm.controls.email.value,
              locale: this._languageConfig.getCurrentLanguage(),
            },
          },
        },
      })
      .pipe(
        tap((result) => {
          const loading = result.loading ?? false;
          this.requestPending.set(loading);
          if (loading) return;

          const acknowledged =
            result.data?.requestPersonalDataExport?.personalDataExportRequestAcknowledgement
              ?.acknowledged ?? false;
          this.requestAcknowledged.set(acknowledged);
          this.requestError.set(!acknowledged);
        }),
        catchError(() => {
          this.requestError.set(true);
          return EMPTY;
        }),
        finalize(() => this.requestPending.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  confirmPersonalDataExport(): void {
    const key = this._confirmationKey();
    if (!key || this.confirmationPending() || this.confirmationSucceeded()) return;

    this.confirmationError.set(false);
    this._confirmPersonalDataExportGQL
      .mutate({ variables: { input: { key } } })
      .pipe(
        tap((result) => {
          const loading = result.loading ?? false;
          this.confirmationPending.set(loading);
          if (loading) return;

          const confirmed =
            result.data?.confirmPersonalDataExport?.personalDataExportConfirmationResult
              ?.confirmed ?? false;
          this.confirmationError.set(!confirmed);
          if (confirmed) {
            this._confirmationKey.set(null);
            this.confirmationSucceeded.set(true);
          }
        }),
        catchError(() => {
          this.confirmationError.set(true);
          return EMPTY;
        }),
        finalize(() => this.confirmationPending.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  private readAndRemoveConfirmationKey(): string | null {
    const browserWindow = this._document.defaultView;
    const fragment = browserWindow?.location.hash;
    if (!browserWindow || !fragment) return null;

    browserWindow.history.replaceState(
      browserWindow.history.state,
      '',
      `${browserWindow.location.pathname}${browserWindow.location.search}`,
    );
    return fragment.slice(1) || null;
  }

}
