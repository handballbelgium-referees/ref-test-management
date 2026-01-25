import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, EMPTY, finalize, tap } from 'rxjs';
import { ReviveRefTestsGQL } from '../../../../../../../../../graphql/generated';
import { Banner } from '../../../../../../../services/banner';

@Component({
  selector: 'app-revive-ref-test-dialog',
  imports: [TranslatePipe],
  templateUrl: './revive-ref-test-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviveRefTestDialog {
  private readonly _reviveRefTestsGQL = inject(ReviveRefTestsGQL);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _bannerService = inject(Banner);
  private readonly _translateService = inject(TranslateService);

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  readonly show = input.required<boolean>();
  readonly refTestId = signal<string>('');
  readonly closeDialog = output<void>();
  readonly cancel = output<void>();

  initialize(refTestId: string): void {
    this.refTestId.set(refTestId);
    this.error.set(null);
  }

  constructor() {
    effect(() => {
      if (this.show()) {
        document.body.style.overflow = 'hidden';
      } else {
        document.body.style.overflow = '';
      }
    });
  }

  protected onConfirm(): void {
    this.error.set(null);

    this._reviveRefTestsGQL
      .mutate({
        variables: {
          input: {
            ids: [this.refTestId()],
          },
        },
      })
      .pipe(
        tap((result) => this.loading.set(result.loading ?? false)),
        tap((result) => {
          const reviveResult = result.data?.reviveRefTests.reviveRefTestsResult;
          if (reviveResult?.errors && reviveResult.errors.length > 0) {
            const error = reviveResult.errors[0];
            this.error.set(error.errorMessage);
          } else if (reviveResult?.successfullyRevived === 1) {
            this._bannerService.success(
              this._translateService.instant('ref_tests.detail.revive.success'),
            );
            this.closeDialog.emit();
          }
        }),
        catchError(() => {
          this.error.set(this._translateService.instant('ref_tests.detail.revive.error'));
          return EMPTY;
        }),
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }
}
