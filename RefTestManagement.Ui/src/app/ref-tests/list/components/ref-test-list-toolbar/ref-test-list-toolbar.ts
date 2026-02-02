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
  host: {
    class: 'block',
  },
})
export class RefTestListToolbar {
  readonly visibleColumns = input.required<Set<string>>();
  readonly showColumnMenu = input.required<boolean>();
  readonly searchTerm = input.required<string>();

  protected readonly createClick = output<void>();
  protected readonly searchInput = output<Event>();
  protected readonly toggleMenuClick = output<void>();
  protected readonly toggleColumnClick = output<string>();
}
