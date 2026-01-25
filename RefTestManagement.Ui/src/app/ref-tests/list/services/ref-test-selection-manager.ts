import { computed, Injectable, signal } from '@angular/core';
import { RefTestStatus } from '../../../../../graphql/generated';
import { IParticipantInfo, RefTestNode } from './types';

/**
 * Service responsible for managing selection state and computing selection summaries
 * for the ref test list component.
 */
@Injectable()
export class RefTestSelectionManager {
  // ========================================================================
  // STATE
  // ========================================================================

  /**
   * Set of currently selected ref test IDs
   */
  readonly selectedIds = signal<Set<string>>(new Set());

  // ========================================================================
  // SELECTION OPERATIONS
  // ========================================================================

  /**
   * Toggle selection for a single ref test
   */
  toggleSelection(refTestId: string): void {
    this.selectedIds.update((ids) => {
      const newIds = new Set(ids);
      if (newIds.has(refTestId)) {
        newIds.delete(refTestId);
      } else {
        newIds.add(refTestId);
      }
      return newIds;
    });
  }

  /**
   * Select or deselect all ref tests
   */
  toggleSelectAll(refTests: RefTestNode[]): void {
    const allIds = refTests.map((t) => t.id);
    const allCurrentlySelected =
      allIds.length > 0 && allIds.every((id) => this.selectedIds().has(id));

    if (allCurrentlySelected) {
      this.selectedIds.set(new Set());
    } else {
      this.selectedIds.set(new Set(allIds));
    }
  }

  /**
   * Clear all selections
   */
  clearSelection(): void {
    this.selectedIds.set(new Set());
  }

  /**
   * Check if a ref test is selected
   */
  isSelected(refTestId: string): boolean {
    return this.selectedIds().has(refTestId);
  }

  // ========================================================================
  // COMPUTED SELECTION STATE
  // ========================================================================

  /**
   * Get the count of selected ref tests
   */
  selectedCount = computed(() => this.selectedIds().size);

  /**
   * Create computed for whether all ref tests are selected
   */
  createAllSelectedComputed(refTests: () => RefTestNode[], totalCount: () => number) {
    return computed(() => {
      const tests = refTests();
      const selected = this.selectedIds();
      const total = totalCount();

      if (selected.size > 0 && selected.size === total) {
        return true;
      }

      return tests.length > 0 && tests.every((t) => selected.has(t.id));
    });
  }

  /**
   * Create computed for whether some (but not all) ref tests are selected
   */
  createSomeSelectedComputed(refTests: () => RefTestNode[], allSelected: () => boolean) {
    return computed(() => {
      const tests = refTests();
      const selected = this.selectedIds();
      return tests.some((t) => selected.has(t.id)) && !allSelected();
    });
  }

  /**
   * Create computed for checking if any completed ref tests are selected
   */
  createHasCompletedSelectedComputed(allRefTests: () => RefTestNode[]) {
    return computed(() => {
      const selected = this.selectedIds();
      const allTests = allRefTests();
      return allTests.some((t) => selected.has(t.id) && t.status === RefTestStatus.Completed);
    });
  }

  /**
   * Create computed for checking if any pending ref tests are selected
   */
  createHasPendingSelectedComputed(allRefTests: () => RefTestNode[]) {
    return computed(() => {
      const selected = this.selectedIds();
      const allTests = allRefTests();
      return allTests.some((t) => selected.has(t.id) && t.status === RefTestStatus.Pending);
    });
  }

  // ========================================================================
  // SELECTION SUMMARIES
  // ========================================================================

  /**
   * Create computed for invitation summary (pending ref tests)
   */
  createInvitationSummaryComputed(allRefTests: () => RefTestNode[]) {
    return computed(() => {
      const selected = this.selectedIds();
      const allTests = allRefTests();
      const pendingSelected = allTests.filter(
        (t) => selected.has(t.id) && t.status === RefTestStatus.Pending,
      );

      return {
        newInvitations: this.mapToParticipantInfo(pendingSelected.filter((t) => !t.invitationSent)),
        resendInvitations: this.mapToParticipantInfo(
          pendingSelected.filter((t) => t.invitationSent),
        ),
      };
    });
  }

  /**
   * Create computed for results summary (completed ref tests)
   */
  createResultsSummaryComputed(allRefTests: () => RefTestNode[]) {
    return computed(() => {
      const selected = this.selectedIds();
      const allTests = allRefTests();
      const completedSelected = allTests.filter(
        (t) => selected.has(t.id) && t.status === RefTestStatus.Completed,
      );

      return {
        newResults: this.mapToParticipantInfo(completedSelected.filter((t) => !t.resultsSent)),
        resendResults: this.mapToParticipantInfo(completedSelected.filter((t) => t.resultsSent)),
      };
    });
  }

  /**
   * Create computed for ref tests to delete summary
   */
  createRefTestsToDeleteComputed(allRefTests: () => RefTestNode[]) {
    return computed(() => {
      const selected = this.selectedIds();
      const allTests = allRefTests();
      return this.mapToParticipantInfo(allTests.filter((t) => selected.has(t.id)));
    });
  }

  /**
   * Create computed for ref tests to reset (any status except Pending)
   */
  createRefTestsToResetComputed(allRefTests: () => RefTestNode[]) {
    return computed(() => {
      const selected = this.selectedIds();
      const allTests = allRefTests();
      return this.mapToParticipantInfo(
        allTests.filter(
          (t) =>
            selected.has(t.id) &&
            (t.status === RefTestStatus.InProgress || t.status === RefTestStatus.Completed),
        ),
      );
    });
  }

  /**
   * Create computed for ref tests to revive (Expired only)
   */
  createRefTestsToReviveComputed(allRefTests: () => RefTestNode[]) {
    return computed(() => {
      const selected = this.selectedIds();
      const allTests = allRefTests();
      return this.mapToParticipantInfo(
        allTests.filter((t) => selected.has(t.id) && t.status === RefTestStatus.Expired),
      );
    });
  }

  /**
   * Create computed for report summary
   */
  createReportSummaryComputed(allRefTests: () => RefTestNode[]) {
    return computed(() => {
      const selected = this.selectedIds();
      const allTests = allRefTests();
      return {
        refTests: this.mapToParticipantInfo(allTests.filter((t) => selected.has(t.id))),
      };
    });
  }

  // ========================================================================
  // HELPER METHODS
  // ========================================================================

  private mapToParticipantInfo(refTests: RefTestNode[]): IParticipantInfo[] {
    return refTests.map((t) => ({
      name: t.name || '',
      email: t.email || '',
    }));
  }
}
