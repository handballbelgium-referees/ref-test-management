import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ColumnVisibilityMenu } from '../column-visibility-menu/column-visibility-menu';

/**
 * Toolbar component for ref test list actions.
 * Includes create button, search bar, and column visibility toggle.
 */
@Component({
  selector: 'app-ref-test-list-toolbar',
  imports: [TranslatePipe, ColumnVisibilityMenu],
  templateUrl: './ref-test-list-toolbar.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestListToolbar {
  readonly visibleColumns = input.required<Set<string>>();
  readonly showColumnMenu = input.required<boolean>();

  readonly createClick = output<void>();
  readonly searchInput = output<Event>();
  readonly toggleMenuClick = output<void>();
  readonly toggleColumnClick = output<string>();
}
