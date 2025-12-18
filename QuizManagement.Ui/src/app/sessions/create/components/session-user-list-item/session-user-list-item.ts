import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Field } from '@angular/forms/signals';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-session-user-list-item',
  imports: [TranslatePipe, Field],
  templateUrl: './session-user-list-item.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class SessionUserListItem {
  readonly userFormControl = input.required<any>();
  readonly index = input.required<number>();
  readonly showRemove = input<boolean>(true);

  readonly remove = output<void>();
}
