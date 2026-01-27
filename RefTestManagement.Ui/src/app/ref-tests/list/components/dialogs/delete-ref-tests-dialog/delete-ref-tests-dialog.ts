import { ChangeDetectionStrategy, Component, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface IRefTestInfo {
  name: string;
  email: string;
}

@Component({
  selector: 'app-delete-ref-tests-dialog',
  imports: [TranslatePipe],
  templateUrl: './delete-ref-tests-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'host',
  },
})
export class DeleteRefTestsDialog {
  readonly show = input.required<boolean>();
  readonly refTests = input.required<IRefTestInfo[]>();

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
