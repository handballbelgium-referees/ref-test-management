import { Injectable, signal } from '@angular/core';
import { COLUMNS } from './constants';

/**
 * Service responsible for managing column visibility state for the ref test list table.
 * Handles which columns are shown/hidden and the column menu state.
 */
@Injectable()
export class ColumnVisibilityManager {
  // ========================================================================
  // STATE
  // ========================================================================

  /**
   * Set of currently visible column identifiers
   */
  readonly visibleColumns = signal(
    new Set<string>([
      COLUMNS.TITLE,
      COLUMNS.PARTICIPANT,
      COLUMNS.STATUS,
      COLUMNS.QUESTIONS,
      COLUMNS.MAX_TIME,
      COLUMNS.SCORE,
      COLUMNS.INVITATION,
      COLUMNS.RESULTS,
      COLUMNS.STARTED,
      COLUMNS.COMPLETED,
    ]),
  );

  /**
   * Whether the column visibility menu is shown
   */
  readonly showColumnMenu = signal(false);

  // ========================================================================
  // COLUMN VISIBILITY OPERATIONS
  // ========================================================================

  /**
   * Check if a column is currently visible
   */
  isColumnVisible(column: string): boolean {
    return this.visibleColumns().has(column);
  }

  /**
   * Toggle visibility of a column
   */
  toggleColumn(column: string): void {
    this.visibleColumns.update((cols) => {
      const newCols = new Set(cols);
      if (newCols.has(column)) {
        newCols.delete(column);
      } else {
        newCols.add(column);
      }
      return newCols;
    });
  }

  /**
   * Toggle the column visibility menu
   */
  toggleColumnMenu(): void {
    this.showColumnMenu.update((show) => !show);
  }

  /**
   * Show all columns
   */
  showAllColumns(): void {
    this.visibleColumns.set(
      new Set([
        COLUMNS.TITLE,
        COLUMNS.PARTICIPANT,
        COLUMNS.STATUS,
        COLUMNS.QUESTIONS,
        COLUMNS.MAX_TIME,
        COLUMNS.SCORE,
        COLUMNS.INVITATION,
        COLUMNS.RESULTS,
        COLUMNS.STARTED,
        COLUMNS.COMPLETED,
      ]),
    );
  }

  /**
   * Hide all columns except required ones (title and participant)
   */
  showMinimalColumns(): void {
    this.visibleColumns.set(new Set([COLUMNS.TITLE, COLUMNS.PARTICIPANT]));
  }

  /**
   * Reset to default column visibility
   */
  resetToDefault(): void {
    this.showAllColumns();
  }
}
