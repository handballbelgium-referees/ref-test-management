import { DOCUMENT } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { lastValueFrom } from 'rxjs';
import { ConfirmPrivacyWithdrawalGQL } from '../../../../../../graphql/generated';

@Component({
  selector: 'app-privacy-withdrawal-confirmation',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './privacy-withdrawal-confirmation.html',
  host: {
    class: 'block',
  },
})
export class PrivacyWithdrawalConfirmation {
  private readonly _document = inject(DOCUMENT);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _confirmPrivacyWithdrawalGQL = inject(ConfirmPrivacyWithdrawalGQL);
  private readonly _confirmationKey = signal(this.readAndRemoveConfirmationKey());

  readonly confirmationPending = signal(false);
  readonly confirmationError = signal(false);
  readonly confirmationSucceeded = signal(false);
  readonly hasConfirmation = this._confirmationKey.asReadonly();

  async confirmWithdrawal(): Promise<void> {
    const key = this._confirmationKey();
    if (!key || this.confirmationPending() || this.confirmationSucceeded()) return;

    this.confirmationError.set(false);
    this.confirmationPending.set(true);

    try {
      const result = await lastValueFrom(
        this._confirmPrivacyWithdrawalGQL
          .mutate({ variables: { input: { key } } })
          .pipe(takeUntilDestroyed(this._destroyRef)),
      );
      const accepted =
        result.data?.confirmPrivacyWithdrawal?.privacyWithdrawalConfirmationResult?.accepted ??
        false;
      this.confirmationError.set(!accepted);
      if (accepted) {
        this._confirmationKey.set(null);
        this.confirmationSucceeded.set(true);
      }
    } catch {
      this.confirmationError.set(true);
    } finally {
      this.confirmationPending.set(false);
    }
  }

  private readAndRemoveConfirmationKey(): string | null {
    const browserWindow = this._document.defaultView;
    const fragment = browserWindow?.location.hash;
    if (!browserWindow || !fragment) return null;

    const key = fragment.slice(1);
    browserWindow.history.replaceState(
      browserWindow.history.state,
      '',
      `${browserWindow.location.pathname}${browserWindow.location.search}`,
    );
    return key || null;
  }
}
