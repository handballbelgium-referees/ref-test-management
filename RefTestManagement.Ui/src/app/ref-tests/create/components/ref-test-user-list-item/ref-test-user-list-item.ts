import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormField } from '@angular/forms/signals';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-ref-test-user-list-item',
  imports: [TranslatePipe, FormField],
  templateUrl: './ref-test-user-list-item.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class RefTestUserListItem {
  readonly userFormControl = input.required<any>();
  readonly index = input.required<number>();
  readonly showRemove = input<boolean>(true);

  readonly remove = output<void>();
}
