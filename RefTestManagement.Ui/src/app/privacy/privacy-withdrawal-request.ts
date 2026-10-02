import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { email, form, FormField, required, submit } from '@angular/forms/signals';
import { TranslatePipe } from '@ngx-translate/core';
import { lastValueFrom } from 'rxjs';
import { RequestPrivacyWithdrawalGQL } from '../../../graphql/generated';

@Component({
  selector: 'app-privacy-withdrawal-request',
  imports: [FormField, TranslatePipe],
  templateUrl: './privacy-withdrawal-request.html',
  host: {
    class: 'block',
  },
})
export class PrivacyWithdrawalRequest {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _requestPrivacyWithdrawalGQL = inject(RequestPrivacyWithdrawalGQL);

  private readonly _requestModel = signal({ email: '' });
  readonly requestForm = form(this._requestModel, (schemaPath) => {
    required(schemaPath.email, { message: 'privacy.consentWithdrawal.emailRequired' });
    email(schemaPath.email, { message: 'privacy.consentWithdrawal.emailInvalid' });
  });
  readonly requestPending = signal(false);
  readonly requestAcknowledged = signal(false);
  readonly requestError = signal(false);

  async requestWithdrawal(): Promise<void> {
    if (this.requestPending() || this.requestAcknowledged()) return;

    this.requestError.set(false);

    await submit(this.requestForm, async () => {
      this.requestPending.set(true);
      try {
        const result = await lastValueFrom(
          this._requestPrivacyWithdrawalGQL
            .mutate({
              variables: {
                input: {
                  input: {
                    email: this._requestModel().email,
                  },
                },
              },
            })
            .pipe(takeUntilDestroyed(this._destroyRef)),
        );
        const acknowledged =
          result.data?.requestPrivacyWithdrawal?.privacyWithdrawalRequestAcknowledgement
            ?.acknowledged ?? false;
        this.requestAcknowledged.set(acknowledged);
        this.requestError.set(!acknowledged);
      } catch {
        this.requestError.set(true);
      } finally {
        this.requestPending.set(false);
      }
    });
  }
}
