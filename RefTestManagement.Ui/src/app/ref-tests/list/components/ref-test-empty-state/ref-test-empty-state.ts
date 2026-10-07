import { Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Empty state component displayed when no ref tests exist.
 * Provides a call to action to create the first ref test.
 */
@Component({
  selector: 'app-ref-test-empty-state',
  imports: [TranslatePipe],
  templateUrl: './ref-test-empty-state.html',
  host: {
    class: 'block',
  },
})
export class RefTestEmptyState {
  readonly filtered = input(false);
  protected readonly createClick = output<void>();
  protected readonly clearFiltersClick = output<void>();
}
