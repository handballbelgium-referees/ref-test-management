import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestNode } from '../../services/types';
import { RefTestMobileCard } from '../ref-test-display/ref-test-mobile-card/ref-test-mobile-card';

/**
 * Mobile list view for ref tests with select all header.
 * Displays ref tests in a card-based layout optimized for mobile devices.
 */
@Component({
  selector: 'app-ref-test-mobile-list',
  imports: [TranslatePipe, RefTestMobileCard],
  templateUrl: './ref-test-mobile-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestMobileList {
  readonly refTests = input.required<RefTestNode[]>();
  readonly visibleColumns = input.required<Set<string>>();
  readonly passingPercentage = input.required<number>();
  readonly allSelected = input.required<boolean>();
  readonly someSelected = input.required<boolean>();
  readonly selectedCount = input.required<number>();
  readonly selectedIds = input.required<Set<string>>();

  readonly toggleSelectAll = output<void>();
  readonly toggleSelection = output<string>();

  isSelected(id: string): boolean {
    return this.selectedIds().has(id);
  }
}
