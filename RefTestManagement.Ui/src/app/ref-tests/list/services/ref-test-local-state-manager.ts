import { computed, Injectable, signal } from '@angular/core';
import { RefTestNode } from './types';

/**
 * Service responsible for managing local state updates for ref tests,
 * including deleted items, updated invitations, and updated results.
 * This optimistically updates the UI before the server state is refreshed.
 */
@Injectable()
export class RefTestLocalStateManager {
  // ========================================================================
  // STATE
  // ========================================================================

  /**
   * IDs of ref tests that have been deleted (optimistic UI update)
   */
  private readonly _deletedRefTestIds = signal<Set<string>>(new Set());

  /**
   * IDs of ref tests whose invitation status has been updated
   */
  private readonly _updatedInvitationIds = signal<Set<string>>(new Set());

  /**
   * IDs of ref tests whose results status has been updated
   */
  private readonly _updatedResultsIds = signal<Set<string>>(new Set());

  /**
   * Additional ref tests loaded via pagination
   */
  private readonly _additionalLoadedRefTests = signal<RefTestNode[]>([]);

  // ========================================================================
  // OPERATIONS
  // ========================================================================

  /**
   * Mark ref tests as deleted
   */
  markAsDeleted(refTestIds: string[]): void {
    this._deletedRefTestIds.update((ids) => {
      const newIds = new Set(ids);
      refTestIds.forEach((id) => newIds.add(id));
      return newIds;
    });
  }

  /**
   * Mark ref tests as having sent invitations
   */
  markInvitationsSent(refTestIds: string[]): void {
    this._updatedInvitationIds.update((ids) => {
      const newIds = new Set(ids);
      refTestIds.forEach((id) => newIds.add(id));
      return newIds;
    });
  }

  /**
   * Mark ref tests as having sent results
   */
  markResultsSent(refTestIds: string[]): void {
    this._updatedResultsIds.update((ids) => {
      const newIds = new Set(ids);
      refTestIds.forEach((id) => newIds.add(id));
      return newIds;
    });
  }

  /**
   * Add additional ref tests from pagination
   */
  addLoadedRefTests(refTests: RefTestNode[]): void {
    this._additionalLoadedRefTests.update((current) => [...current, ...refTests]);
  }

  /**
   * Get additional loaded ref tests
   */
  getAdditionalLoadedRefTests(): RefTestNode[] {
    return this._additionalLoadedRefTests();
  }

  /**
   * Reset all local state (used when filters change or data is refreshed)
   */
  resetAllState(): void {
    this._deletedRefTestIds.set(new Set());
    this._updatedInvitationIds.set(new Set());
    this._updatedResultsIds.set(new Set());
    this._additionalLoadedRefTests.set([]);
  }

  // ========================================================================
  // COMPUTED STATE APPLICATION
  // ========================================================================

  /**
   * Create a computed that applies local state to a list of ref tests
   */
  createApplyLocalStateComputed(baseRefTests: () => RefTestNode[]) {
    return computed(() => {
      const base = baseRefTests();
      const additional = this._additionalLoadedRefTests();

      // Deduplicate by ID
      const seenIds = new Set<string>();
      const allTests: RefTestNode[] = [];

      for (const test of [...base, ...additional]) {
        if (!seenIds.has(test.id)) {
          seenIds.add(test.id);
          allTests.push(test);
        }
      }

      // Apply local operation state
      const deletedIds = this._deletedRefTestIds();
      const invitationSentIds = this._updatedInvitationIds();
      const resultsSentIds = this._updatedResultsIds();

      return allTests
        .filter((test) => !deletedIds.has(test.id))
        .map((test) => ({
          ...test,
          invitationSent: invitationSentIds.has(test.id) ? true : test.invitationSent,
          resultsSent: resultsSentIds.has(test.id) ? true : test.resultsSent,
        }));
    });
  }
}
