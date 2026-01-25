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
import { FormsModule } from '@angular/forms';
import { disabled, form, FormField } from '@angular/forms/signals';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, EMPTY, finalize, map, tap } from 'rxjs';
import { RefTestResetType, ResetRefTestsGQL } from '../../../../../../../../../graphql/generated';
import { Toast } from '../../../../../../../services/toast';

interface IResetOptions {
  resetType: RefTestResetType;
  regenerateToken: boolean;
}

@Component({
  selector: 'app-reset-ref-test-dialog',
  imports: [TranslatePipe, FormField, FormsModule],
  templateUrl: './reset-ref-test-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResetRefTestDialog {
  private readonly _resetRefTestsGQL = inject(ResetRefTestsGQL);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _toast = inject(Toast);
  private readonly _translateService = inject(TranslateService);

  protected readonly resetOptionsModel = signal<IResetOptions>({
    resetType: RefTestResetType.Soft,
    regenerateToken: false,
  });

  protected readonly isHardReset = computed(() => {
    return this.resetOptionsModel().resetType === RefTestResetType.Hard;
  });

  protected readonly resetOptionsForm = form(this.resetOptionsModel, (schema) => {
    disabled(schema.regenerateToken, () => this.isHardReset());
  });

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  readonly show = input.required<boolean>();
  readonly refTestId = signal<string>('');
  readonly closeDialog = output<void>();
  readonly cancel = output<void>();

  protected readonly RefTestResetType = RefTestResetType;

  initialize(refTestId: string): void {
    this.refTestId.set(refTestId);
    this.resetOptionsModel.set({
      resetType: RefTestResetType.Soft,
      regenerateToken: false,
    });
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

    // Auto-check regenerateToken when Hard Reset is selected, uncheck when Soft Reset is selected
    effect(() => {
      const currentModel = this.resetOptionsModel();
      if (currentModel.resetType === RefTestResetType.Hard && !currentModel.regenerateToken) {
        this.resetOptionsModel.set({
          ...currentModel,
          regenerateToken: true,
        });
      } else if (currentModel.resetType === RefTestResetType.Soft && currentModel.regenerateToken) {
        this.resetOptionsModel.set({
          ...currentModel,
          regenerateToken: false,
        });
      }
    });
  }

  protected onConfirm(): void {
    const options = this.resetOptionsModel();
    this.error.set(null);

    this._resetRefTestsGQL
      .mutate({
        variables: {
          input: {
            ids: [this.refTestId()],
            resetType: options.resetType,
            regenerateToken: options.regenerateToken || undefined,
          },
        },
      })
      .pipe(
        tap((result) => this.loading.set(result.loading ?? false)),
        map((result) => result.data?.resetRefTests),
        tap((data) => {
          const resetResult = data?.resetRefTestsResult;
          if (resetResult?.errors && resetResult.errors.length > 0) {
            const error = resetResult.errors[0];
            this.error.set(error.errorMessage);
          } else if (resetResult?.successfullyReset === 1) {
            this._toast.success(this._translateService.instant('ref_tests.detail.reset.success'));
            this.closeDialog.emit();
          }
        }),
        catchError(() => {
          this.error.set(this._translateService.instant('ref_tests.detail.reset.error'));
          return EMPTY;
        }),
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }
}
