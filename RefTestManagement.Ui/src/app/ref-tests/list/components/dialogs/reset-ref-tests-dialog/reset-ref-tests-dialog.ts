import {
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
import { RefTestResetType } from '../../../../../../../graphql/generated';
import { IsolatedBannerManager } from '../../../../../services/banner';
import { Banner } from '../../../../../shared/components/banner/banner';

interface IRefTestInfo {
  name: string;
  email: string;
}

interface IResetOptions {
  resetType: RefTestResetType;
  regenerateToken: boolean;
}

@Component({
  selector: 'app-reset-ref-tests-dialog',
  imports: [TranslatePipe, FormsModule, FormField, Banner],
  templateUrl: './reset-ref-tests-dialog.html',
  host: {
    class: 'host',
  },
})
export class ResetRefTestsDialog {
  readonly loading = input.required<boolean>();
  readonly show = input.required<boolean>();
  readonly refTests = input.required<IRefTestInfo[]>();
  readonly bannerManager = input.required<IsolatedBannerManager>();

  protected readonly confirm = output<IResetOptions>();
  protected readonly cancel = output<void>();

  protected readonly resetOptionsModel = signal<IResetOptions>({
    resetType: 'SOFT',
    regenerateToken: false,
  });

  protected readonly isHardReset = computed(() => {
    return this.resetOptionsModel().resetType === 'HARD';
  });

  protected readonly resetOptionsForm = form(this.resetOptionsModel, (schema) => {
    disabled(schema.regenerateToken, () => this.isHardReset());
  });

  constructor() {
    effect(() => {
      if (this.show()) {
        document.body.style.overflow = 'hidden';
        // Reset form when dialog opens
        this.resetOptionsModel.set({
          resetType: 'SOFT',
          regenerateToken: false,
        });
      } else {
        document.body.style.overflow = '';
      }
    });
  }

  protected onConfirm(): void {
    this.confirm.emit(this.resetOptionsModel());
  }
}
