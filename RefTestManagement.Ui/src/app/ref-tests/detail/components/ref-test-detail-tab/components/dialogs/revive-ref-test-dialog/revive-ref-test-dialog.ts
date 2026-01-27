import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import {
  Banner as BannerService,
  IsolatedBannerManager,
} from '../../../../../../../services/banner';
import { Banner } from '../../../../../../../shared/components/banner/banner';

@Component({
  selector: 'app-revive-ref-test-dialog',
  imports: [TranslatePipe, Banner],
  templateUrl: './revive-ref-test-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviveRefTestDialog {
  // Create isolated banner manager for this dialog
  protected readonly bannerManager = inject(BannerService).createIsolated();

  readonly show = input.required<boolean>();
  readonly refTestId = signal<string>('');
  readonly loading = input<boolean>(false);
  readonly confirm = output<IsolatedBannerManager>();
  readonly cancel = output<void>();

  initialize(refTestId: string): void {
    this.refTestId.set(refTestId);
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
    this.confirm.emit(this.bannerManager);
  }
}
