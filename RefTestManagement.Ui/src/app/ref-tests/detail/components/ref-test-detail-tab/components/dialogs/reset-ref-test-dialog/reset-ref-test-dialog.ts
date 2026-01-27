import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { disabled, form, FormField } from '@angular/forms/signals';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestResetType } from '../../../../../../../../../graphql/generated';
import {
  Banner as BannerService,
  IsolatedBannerManager,
} from '../../../../../../../services/banner';
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
  // Create isolated banner manager for this dialog
  protected readonly bannerManager = inject(BannerService).createIsolated();

  protected readonly resetOptionsModel = signal<IResetOptions>({
    resetType: RefTestResetType.Soft,
    regenerateToken: false,
  });

  private readonly _previousResetType = signal<RefTestResetType>(RefTestResetType.Soft);

  protected readonly isHardReset = computed(() => {
    return this.resetOptionsModel().resetType === RefTestResetType.Hard;
  });

  protected readonly resetOptionsForm = form(this.resetOptionsModel, (schema) => {
    disabled(schema.regenerateToken, () => this.isHardReset());
  });

  readonly show = input.required<boolean>();
  readonly refTestId = signal<string>('');
  readonly loading = input<boolean>(false);
  readonly confirm = output<{ options: IResetOptions; bannerManager: IsolatedBannerManager }>();
  readonly cancel = output<void>();

  protected readonly RefTestResetType = RefTestResetType;

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

        if (currentModel.resetType === RefTestResetType.Hard) {
          this.resetOptionsModel.set({
            ...currentModel,
            regenerateToken: true,
          });
        } else if (currentModel.resetType === RefTestResetType.Soft) {
          this.resetOptionsModel.set({
            ...currentModel,
            regenerateToken: false,
          });
        }
      }
    });
  }

  onConfirm(): void {
    if (this.resetOptionsForm().valid()) {
      this.confirm.emit({ options: this.resetOptionsModel(), bannerManager: this.bannerManager });
    }
  }
}
