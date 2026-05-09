import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ColumnVisibilityMenu } from '../column-visibility-menu/column-visibility-menu';
import { HasPermission } from '../../../../auth/directives/has-permission.directive';
import { Permissions } from '../../../../auth/models/permissions';

/**
 * Toolbar component for ref test list actions.
 * Includes create button, search bar, and column visibility toggle.
 */
@Component({
  selector: 'app-ref-test-list-toolbar',
  imports: [TranslatePipe, ColumnVisibilityMenu, HasPermission],
  templateUrl: './ref-test-list-toolbar.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class RefTestListToolbar {
  readonly visibleColumns = input.required<Set<string>>();
  readonly showColumnMenu = input.required<boolean>();
  readonly searchTerm = input.required<string>();

  protected readonly Permissions = Permissions;

  protected readonly createClick = output<void>();
  protected readonly searchInput = output<Event>();
  protected readonly toggleMenuClick = output<void>();
  protected readonly toggleColumnClick = output<string>();
}
