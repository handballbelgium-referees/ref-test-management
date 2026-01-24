import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Empty state component displayed when no ref tests exist.
 * Provides a call to action to create the first ref test.
 */
@Component({
  selector: 'app-ref-test-empty-state',
  imports: [TranslatePipe],
  templateUrl: './ref-test-empty-state.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestEmptyState {
  readonly createClick = output<void>();
}
