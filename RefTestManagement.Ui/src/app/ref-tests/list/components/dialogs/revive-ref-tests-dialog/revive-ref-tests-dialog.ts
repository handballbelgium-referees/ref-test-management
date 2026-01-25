import { ChangeDetectionStrategy, Component, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface IRefTestInfo {
  name: string;
  email: string;
}

@Component({
  selector: 'app-revive-ref-tests-dialog',
  imports: [TranslatePipe],
  templateUrl: './revive-ref-tests-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'host',
  },
})
export class ReviveRefTestsDialog {
  readonly show = input.required<boolean>();
  readonly refTests = input.required<IRefTestInfo[]>();

  readonly confirm = output<void>();
  readonly cancel = output<void>();

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
