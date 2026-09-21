import { Component, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Dialog } from '../../../../shared/components/dialog/dialog';

@Component({
  selector: 'app-leave-ref-test-dialog',
  templateUrl: './leave-ref-test-dialog.html',
  imports: [TranslatePipe, Dialog],
  host: {
    class: 'block',
  },
})
export class LeaveRefTestDialog {
  readonly show = input.required<boolean>();
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
