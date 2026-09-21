import { Component, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Dialog } from '../../../shared/components/dialog/dialog';

@Component({
  selector: 'app-withdraw-consent-dialog',
  templateUrl: './withdraw-consent-dialog.html',
  imports: [TranslatePipe, Dialog],
  host: {
    class: 'block',
  },
})
export class WithdrawConsentDialog {
  readonly show = input.required<boolean>();
  readonly pending = input(false);
  readonly error = input(false);
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
