import { Component, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { IsolatedBannerManager } from '../../../../../../../services/banner';
import { Banner } from '../../../../../../../shared/components/banner/banner';
import { Dialog } from '../../../../../../../shared/components/dialog/dialog';

@Component({
  selector: 'app-revive-ref-test-dialog',
  imports: [TranslatePipe, Banner, Dialog],
  templateUrl: './revive-ref-test-dialog.html',
})
export class ReviveRefTestDialog {
  readonly loading = input.required<boolean>();
  readonly show = input.required<boolean>();
  readonly bannerManager = input.required<IsolatedBannerManager>();
  protected readonly confirm = output<void>();
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
}
