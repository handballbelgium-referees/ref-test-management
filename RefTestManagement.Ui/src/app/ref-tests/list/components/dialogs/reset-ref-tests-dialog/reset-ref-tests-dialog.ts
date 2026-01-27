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
import { RefTestResetType } from '../../../../../../../graphql/generated';

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
  imports: [TranslatePipe, FormsModule, FormField],
  templateUrl: './reset-ref-tests-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'host',
  },
})
export class ResetRefTestsDialog {
  readonly show = input.required<boolean>();
  readonly refTests = input.required<IRefTestInfo[]>();

  protected readonly confirm = output<IResetOptions>();
  protected readonly cancel = output<void>();

  protected readonly RefTestResetType = RefTestResetType;

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

  constructor() {
    effect(() => {
      if (this.show()) {
        document.body.style.overflow = 'hidden';
        // Reset form when dialog opens
        this.resetOptionsModel.set({
          resetType: RefTestResetType.Soft,
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
