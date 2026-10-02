import { DOCUMENT } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, EMPTY, finalize, tap } from 'rxjs';
import { ConfirmPersonalDataExportGQL } from '../../../graphql/generated';

@Component({
  selector: 'app-personal-data-export-confirmation',
  imports: [TranslatePipe],
  templateUrl: './personal-data-export-confirmation.html',
  host: {
    class: 'block',
  },
})
export class PersonalDataExportConfirmation {
  private readonly _document = inject(DOCUMENT);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _confirmPersonalDataExportGQL = inject(ConfirmPersonalDataExportGQL);
  private readonly _confirmationKey = signal(this.readAndRemoveConfirmationKey());

  readonly confirmationPending = signal(false);
  readonly confirmationError = signal(false);
  readonly confirmationSucceeded = signal(false);
  readonly hasConfirmation = computed(
    () => this._confirmationKey() !== null || this.confirmationSucceeded(),
  );

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
