import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { disabled, form, FormField } from '@angular/forms/signals';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestResetType } from '../../../../../../../../../graphql/generated';
import { IsolatedBannerManager } from '../../../../../../../services/banner';
import { Banner } from '../../../../../../../shared/components/banner/banner';

interface IResetOptions {
  resetType: RefTestResetType;
  regenerateToken: boolean;
}

@Component({
  selector: 'app-reset-ref-test-dialog',
  imports: [TranslatePipe, FormField, FormsModule, Banner],
  templateUrl: './reset-ref-test-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResetRefTestDialog {
  protected readonly resetOptionsModel = signal<IResetOptions>({
    resetType: 'SOFT',
    regenerateToken: false,
  });

  private readonly _previousResetType = signal<RefTestResetType>('SOFT');

  protected readonly isHardReset = computed(() => {
    return this.resetOptionsModel().resetType === 'HARD';
  });

  protected readonly resetOptionsForm = form(this.resetOptionsModel, (schema) => {
    disabled(schema.regenerateToken, () => this.isHardReset());
  });

  readonly loading = input.required<boolean>();
  readonly show = input.required<boolean>();
  readonly bannerManager = input.required<IsolatedBannerManager>();
  protected readonly confirm = output<IResetOptions>();
  protected readonly cancel = output<void>();

  constructor() {
    effect(() => {
      if (this.show()) {
        document.body.style.overflow = 'hidden';
      } else {
        document.body.style.overflow = '';
      }
    });

    // Auto-check regenerateToken when Hard Reset is selected, uncheck when switching to Soft Reset
    effect(() => {
      const currentModel = this.resetOptionsModel();
      const previousType = this._previousResetType();

      // Only act when reset type actually changes
      if (currentModel.resetType !== previousType) {
        this._previousResetType.set(currentModel.resetType);

        if (currentModel.resetType === 'HARD') {
          this.resetOptionsModel.set({
            ...currentModel,
            regenerateToken: true,
          });
        } else if (currentModel.resetType === 'SOFT') {
          this.resetOptionsModel.set({
            ...currentModel,
            regenerateToken: false,
          });
        }
      }
    });
  }

  protected onConfirm(): void {
    if (this.resetOptionsForm().invalid()) {
      this.resetOptionsForm().markAsTouched();
      return;
    }

    this.confirm.emit(this.resetOptionsModel());
  }
}
