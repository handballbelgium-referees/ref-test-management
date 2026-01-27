import { ChangeDetectionStrategy, Component, effect, inject, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import {
  Banner as BannerService,
  IsolatedBannerManager,
} from '../../../../../../../services/banner';
import { Banner } from '../../../../../../../shared/components/banner/banner';

@Component({
  selector: 'app-regenerate-token-dialog',
  imports: [TranslatePipe, Banner],
  templateUrl: './regenerate-token-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegenerateTokenDialog {
  // Create isolated banner manager for this dialog
  protected readonly bannerManager = inject(BannerService).createIsolated();

  readonly loading = input.required<boolean>();
  readonly show = input.required<boolean>();
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
