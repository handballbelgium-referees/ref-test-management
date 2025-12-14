import { ChangeDetectionStrategy, Component, effect, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface ISessionInfo {
  name: string;
  email: string;
}

@Component({
  selector: 'app-delete-sessions-dialog',
  imports: [TranslatePipe],
  templateUrl: './delete-sessions-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeleteSessionsDialog {
  readonly show = input.required<boolean>();
  readonly sessions = input.required<ISessionInfo[]>();

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
