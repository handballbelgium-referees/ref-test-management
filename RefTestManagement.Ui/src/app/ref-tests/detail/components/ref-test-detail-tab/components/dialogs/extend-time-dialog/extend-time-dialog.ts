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
import { form, FormField, min, required } from '@angular/forms/signals';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, EMPTY, finalize, tap } from 'rxjs';
import { ExtendRefTestTimeGQL } from '../../../../../../../../../graphql/generated';
import { Toast } from '../../../../../../../services/toast';

interface IExtendTimeData {
  additionalMinutes: number;
}

@Component({
  selector: 'app-extend-time-dialog',
  imports: [TranslatePipe, FormField],
  templateUrl: './extend-time-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExtendTimeDialog {
  private readonly _extendRefTestTimeGQL = inject(ExtendRefTestTimeGQL);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _toast = inject(Toast);
  private readonly _translateService = inject(TranslateService);

  protected readonly extendTimeModel = signal<IExtendTimeData>({
    additionalMinutes: 15,
  });

  protected readonly extendTimeForm = form(this.extendTimeModel, (schema) => {
    required(schema.additionalMinutes, {
      message: 'ref_tests.detail.extend_time.additional_minutes_required',
    });
    min(schema.additionalMinutes, 1, {
      message: 'ref_tests.detail.extend_time.additional_minutes_min',
    });
  });

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  readonly show = input.required<boolean>();
  readonly refTestId = signal<string>('');
  readonly currentMaxTime = signal<number>(0);
  readonly closeDialog = output<void>();
  readonly cancel = output<void>();

  protected readonly canSave = computed(() => {
    return this.extendTimeForm().valid() && !this.loading();
  });

  protected readonly newMaxTime = computed(() => {
    return this.currentMaxTime() + this.extendTimeModel().additionalMinutes;
  });

  initialize(refTestId: string, currentMaxTime: number): void {
    this.refTestId.set(refTestId);
    this.currentMaxTime.set(currentMaxTime);
    this.extendTimeModel.set({ additionalMinutes: 15 });
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

  protected onSave(): void {
    if (this.extendTimeForm().invalid()) {
      this.extendTimeForm().markAsTouched();
      return;
    }

    const data = this.extendTimeModel();
    this.error.set(null);

    this._extendRefTestTimeGQL
      .mutate({
        variables: {
          input: {
            id: this.refTestId(),
            additionalMinutes: data.additionalMinutes,
          },
        },
      })
      .pipe(
        tap((result) => this.loading.set(result.loading ?? false)),
        tap((result) => {
          if (result.data?.extendRefTestTime.errors?.length) {
            const error = result.data.extendRefTestTime.errors[0];
            this.error.set(this._translateService.instant(error.message));
          } else if (result.data?.extendRefTestTime.refTest) {
            this._toast.success(
              this._translateService.instant('ref_tests.detail.extend_time.success'),
            );
            this.closeDialog.emit();
          }
        }),
        catchError(() => {
          this.error.set(this._translateService.instant('ref_tests.detail.extend_time.error'));
          return EMPTY;
        }),
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }
}
