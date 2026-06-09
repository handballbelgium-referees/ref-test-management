import { Component, input, output } from '@angular/core';
import { FieldTree, FormField } from '@angular/forms/signals';
import { TranslatePipe } from '@ngx-translate/core';
import { IUserData } from '../../create-ref-tests';

@Component({
  selector: 'app-ref-test-user-list-item',
  imports: [TranslatePipe, FormField],
  templateUrl: './ref-test-user-list-item.html',
  host: {
    class: 'block',
  },
})
export class RefTestUserListItem {
  readonly userFormControl = input.required<FieldTree<IUserData, number>>();
  readonly index = input.required<number>();
  readonly showRemove = input<boolean>(true);

  protected readonly remove = output<void>();
}
