import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { SortEnumType } from '../../../../../../graphql/generated';
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
})
export class RefTestTable {
  readonly refTests = input.required<RefTestNode[]>();
  readonly visibleColumns = input.required<Set<string>>();
  readonly filter = input.required<IRefTestFilter>();
  readonly passingPercentage = input.required<number>();
  readonly allSelected = input.required<boolean>();
  readonly someSelected = input.required<boolean>();
  readonly selectedIds = input.required<Set<string>>();

  readonly toggleSelectAll = output<void>();
  readonly toggleSelection = output<string>();
  readonly sortColumn = output<SortField>();

  readonly SortEnumType = SortEnumType;

  isColumnVisible(column: string): boolean {
    return this.visibleColumns().has(column);
  }

  getAriaSort(field: SortField): string | null {
    if (this.filter().sortField !== field) return null;
    return this.filter().sortDirection === SortEnumType.Asc ? 'ascending' : 'descending';
  }

  isSelected(id: string): boolean {
    return this.selectedIds().has(id);
  }
}
