import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { IRefTestFilter, RefTestNode, SortField } from '../../services/types';
import { RefTestTableRow } from '../ref-test-display/ref-test-table-row/ref-test-table-row';

/**
 * Desktop table view for ref tests with sortable headers.
 * Displays ref tests in a responsive table with column visibility support.
 */
@Component({
  selector: 'app-ref-test-table',
  imports: [TranslatePipe, RefTestTableRow],
  templateUrl: './ref-test-table.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class RefTestTable {
  readonly refTests = input.required<RefTestNode[]>();
  readonly visibleColumns = input.required<Set<string>>();
  readonly filter = input.required<IRefTestFilter>();
  readonly passingPercentage = input.required<number>();
  readonly allSelected = input.required<boolean>();
  readonly someSelected = input.required<boolean>();
  readonly selectedIds = input.required<Set<string>>();

  protected readonly toggleSelectAll = output<void>();
  protected readonly toggleSelection = output<string>();
  protected readonly sortColumn = output<SortField>();

  protected isColumnVisible(column: string): boolean {
    return this.visibleColumns().has(column);
  }

  protected getAriaSort(field: SortField): string | null {
    if (this.filter().sortField !== field) return null;
    return this.filter().sortDirection === 'ASC' ? 'ascending' : 'descending';
  }

  protected isSelected(id: string): boolean {
    return this.selectedIds().has(id);
  }
}
