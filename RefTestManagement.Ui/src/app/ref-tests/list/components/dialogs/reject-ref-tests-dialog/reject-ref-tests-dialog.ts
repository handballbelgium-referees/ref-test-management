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
import { TranslatePipe } from '@ngx-translate/core';
import { IsolatedBannerManager } from '../../../../../services/banner';
import { Banner } from '../../../../../shared/components/banner/banner';

interface IRefTestInfo {
  name: string;
  email: string;
}

@Component({
  selector: 'app-reject-ref-tests-dialog',
  imports: [TranslatePipe, FormsModule, Banner],
  templateUrl: './reject-ref-tests-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'host',
  },
})
export class RejectRefTestsDialog {
  readonly loading = input.required<boolean>();
  readonly show = input.required<boolean>();
  readonly refTests = input.required<IRefTestInfo[]>();
  readonly bannerManager = input.required<IsolatedBannerManager>();

  protected readonly confirm = output<string>();
  protected readonly cancel = output<void>();

  protected readonly reason = signal('');
  protected readonly touched = signal(false);

  protected readonly isReasonValid = computed(() => this.reason().trim().length > 0);
  protected readonly showValidationError = computed(() => this.touched() && !this.isReasonValid());

  constructor() {
    effect(() => {
      if (this.show()) {
        document.body.style.overflow = 'hidden';
        this.reason.set('');
        this.touched.set(false);
      } else {
        document.body.style.overflow = '';
      }
    });
  }

  protected onConfirm(): void {
    this.touched.set(true);
    if (this.isReasonValid()) {
      this.confirm.emit(this.reason().trim());
    }
  }
}
