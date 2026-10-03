import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { email, form, FormField, required, submit } from '@angular/forms/signals';
import { TranslatePipe } from '@ngx-translate/core';
import { lastValueFrom } from 'rxjs';
import { RequestPersonalDataExportGQL } from '../../../../../../graphql/generated';

@Component({
  selector: 'app-personal-data-export-request',
  imports: [FormField, TranslatePipe],
  templateUrl: './personal-data-export-request.html',
  host: {
    class: 'block',
  },
})
export class PersonalDataExportRequest {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _requestPersonalDataExportGQL = inject(RequestPersonalDataExportGQL);

  private readonly _requestModel = signal({ email: '' });
  readonly requestForm = form(this._requestModel, (schemaPath) => {
    required(schemaPath.email, { message: 'privacy.dataExport.emailRequired' });
    email(schemaPath.email, { message: 'privacy.dataExport.emailInvalid' });
  });
  readonly requestPending = signal(false);
  readonly requestAcknowledged = signal(false);
  readonly requestError = signal(false);

  async requestPersonalDataExport(): Promise<void> {
    if (this.requestPending() || this.requestAcknowledged()) return;

    this.requestAcknowledged.set(false);
    this.requestError.set(false);

    let actionStarted = false;
    try {
      await submit(this.requestForm, async () => {
        actionStarted = true;
        this.requestPending.set(true);
        try {
          const result = await lastValueFrom(
            this._requestPersonalDataExportGQL
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
            result.data?.requestPersonalDataExport?.personalDataExportRequestAcknowledgement
              ?.acknowledged ?? false;
          this.requestAcknowledged.set(acknowledged);
          this.requestError.set(!acknowledged);
        } catch {
          this.requestError.set(true);
        }
      });
    } finally {
      if (actionStarted) {
        this.requestPending.set(false);
      }
    }
  }
}
