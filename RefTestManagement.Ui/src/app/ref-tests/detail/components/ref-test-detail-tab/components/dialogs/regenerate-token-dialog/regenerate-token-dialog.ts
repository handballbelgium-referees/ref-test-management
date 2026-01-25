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
import { catchError, EMPTY, finalize, map, tap } from 'rxjs';
import { RegenerateRefTestTokenGQL } from '../../../../../../../../../graphql/generated';
import { Banner } from '../../../../../../../services/banner';
import { toSnakeCase } from '../../../../../../../shared/utils/string-utils';

@Component({
  selector: 'app-regenerate-token-dialog',
  imports: [TranslatePipe],
  templateUrl: './regenerate-token-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegenerateTokenDialog {
  private readonly _regenerateRefTestTokenGQL = inject(RegenerateRefTestTokenGQL);
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

    this._regenerateRefTestTokenGQL
      .mutate({
        variables: {
          input: {
            refTestId: this.refTestId(),
          },
        },
      })
      .pipe(
        tap((result) => this.loading.set(result.loading ?? false)),
        map((result) => result.data?.regenerateRefTestToken),
        tap((data) => {
          if (data?.errors && data.errors.length > 0) {
            const error = data.errors[0];
            if ('__typename' in error && error.__typename) {
              this.error.set(toSnakeCase(error.__typename));
            }
            return;
          }

          if (data?.refTest) {
            this._bannerService.success(
              this._translateService.instant('ref_tests.detail.regenerate_token.success'),
            );
            this.closeDialog.emit();
          }
        }),
        catchError(() => {
          this.error.set(this._translateService.instant('ref_tests.detail.regenerate_token.error'));
          return EMPTY;
        }),
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }
}
