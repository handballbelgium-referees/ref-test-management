import { Component, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-leave-ref-test-dialog',
  templateUrl: './leave-ref-test-dialog.html',
  imports: [TranslatePipe],
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
