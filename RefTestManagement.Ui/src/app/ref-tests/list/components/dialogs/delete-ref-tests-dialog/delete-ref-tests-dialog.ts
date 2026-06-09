import { Component, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { IsolatedBannerManager } from '../../../../../services/banner';
import { Banner } from '../../../../../shared/components/banner/banner';

interface IRefTestInfo {
  name: string;
  email: string;
}

@Component({
  selector: 'app-delete-ref-tests-dialog',
  imports: [TranslatePipe, Banner],
  templateUrl: './delete-ref-tests-dialog.html',
  host: {
    class: 'host',
  },
})
export class DeleteRefTestsDialog {
  readonly loading = input.required<boolean>();
  readonly show = input.required<boolean>();
  readonly refTests = input.required<IRefTestInfo[]>();
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
