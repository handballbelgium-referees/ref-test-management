import { ChangeDetectionStrategy, Component, effect, inject, input, output } from '@angular/core';
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
  readonly loading = input<boolean>(false);
  protected readonly confirm = output<IsolatedBannerManager>();
  protected readonly cancel = output<void>();

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
