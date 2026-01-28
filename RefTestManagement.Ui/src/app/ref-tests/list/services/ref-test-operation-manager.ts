import { DestroyRef, inject, Injectable, signal, WritableSignal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { RefTestStatus } from '../../../../../graphql/generated';
import { Banner } from '../../../services/banner';
import { createDialogOperation } from '../../../shared/utils/dialog-utils';
import { RefTestData } from '../../services/ref-test-data';
import { RefTestLocalStateManager } from './ref-test-local-state-manager';
import { RefTestSelectionManager } from './ref-test-selection-manager';
import { IResetOptions, RefTestNode } from './types';

/**
 * Service responsible for managing operations on ref tests
 * (delete, send invitations, send results, generate report, reset, revive)
 */
@Injectable()
export class RefTestOperationManager {
  private readonly _bannerService = inject(Banner);
  private readonly _translateService = inject(TranslateService);
  private readonly _dataService = inject(RefTestData);
  private readonly _selectionManager = inject(RefTestSelectionManager);
  private readonly _localStateManager = inject(RefTestLocalStateManager);
  private readonly _destroyRef = inject(DestroyRef);

  // ========================================================================
  // OPERATION STATE
  // ========================================================================
  private readonly _deletingRefTestIds = signal<Set<string>>(new Set());
  private readonly _sendingInvitationIds = signal<Set<string>>(new Set());
  private readonly _sendingResultsIds = signal<Set<string>>(new Set());
  private readonly _resettingRefTestIds = signal<Set<string>>(new Set());
  private readonly _revivingRefTestIds = signal<Set<string>>(new Set());

  // ========================================================================
  // HELPER METHODS
  // ========================================================================
  private updateIds(target: WritableSignal<Set<string>>, ids: string[], add = true) {
    target.update((current) => {
      const next = new Set(current);
      ids.forEach((id) => (add ? next.add(id) : next.delete(id)));
      return next;
    });
  }
  private addIds(target: WritableSignal<Set<string>>, ids: string[]): void {
    this.updateIds(target, ids, true);
  }
  private removeIds(target: WritableSignal<Set<string>>, ids: string[]): void {
    this.updateIds(target, ids, false);
  }

  private getSelectedIds = () => Array.from(this._selectionManager.selectedIds());

  // ========================================================================
  // DIALOG OPERATIONS
  // ========================================================================

  readonly deleteDialog = createDialogOperation((ids, _, bannerManager) => {
    this.addIds(this._deletingRefTestIds, ids);
    return this._dataService.deleteRefTests(ids, this._destroyRef, {
      onSuccess: (deletedIds) => {
        this._localStateManager.markAsDeleted(deletedIds);
        this._selectionManager.clearSelection();
        this._bannerService.success(
          this._translateService.instant('ref_tests.list.delete_success'),
        );
      },
      onError: () =>
        bannerManager?.error(this._translateService.instant('ref_tests.list.delete_error')),
      onComplete: () => this.removeIds(this._deletingRefTestIds, ids),
    });
  }, this.getSelectedIds);

  readonly sendInvitationsDialog = createDialogOperation<RefTestNode[]>(
    (ids, allRefTests, bannerManager) => {
      const filtered = ids.filter(
        (id) => allRefTests.find((t) => t.id === id)?.status === RefTestStatus.Pending,
      );
      this.addIds(this._sendingInvitationIds, filtered);
      return this._dataService.sendInvitations(filtered, this._destroyRef, {
        onSuccess: (sendIds) => {
          this._localStateManager.markInvitationsSent(sendIds);
          this._selectionManager.clearSelection();
          this._bannerService.success(
            this._translateService.instant('ref_tests.list.invitations_sent'),
          );
        },
        onError: () =>
          bannerManager?.error(this._translateService.instant('ref_tests.list.invitations_error')),
        onComplete: () => this.removeIds(this._sendingInvitationIds, filtered),
      });
    },
    this.getSelectedIds,
  );

  readonly sendResultsDialog = createDialogOperation<RefTestNode[]>(
    (ids, allRefTests, bannerManager) => {
      const filtered = ids.filter(
        (id) => allRefTests.find((t) => t.id === id)?.status === RefTestStatus.Completed,
      );
      this.addIds(this._sendingResultsIds, filtered);
      return this._dataService.sendResults(filtered, this._destroyRef, {
        onSuccess: () => {
          this._selectionManager.clearSelection();
          this._bannerService.success(
            this._translateService.instant('ref_tests.list.results_sent'),
          );
        },
        onError: () =>
          bannerManager?.error(this._translateService.instant('ref_tests.list.results_error')),
        onComplete: () => this.removeIds(this._sendingResultsIds, filtered),
      });
    },
    this.getSelectedIds,
  );

  readonly generateReportDialog = createDialogOperation((ids, _, bannerManager) => {
    return this._dataService.generateReport(ids, this._destroyRef, {
      onSuccess: (result) => {
        if (result.success) {
          this._selectionManager.clearSelection();
          this._bannerService.success(
            this._translateService.instant('ref_tests.list.report_success'),
            this._translateService.instant('ref_tests.list.report.ref_test_count', {
              count: result.refTestCount,
            }),
          );
        } else {
          bannerManager?.error(this._translateService.instant('ref_tests.list.report_error'));
        }
      },
      onError: () => {
        bannerManager?.error(this._translateService.instant('ref_tests.list.report_error'));
      },
    });
  }, this.getSelectedIds);

  readonly resetDialog = createDialogOperation<IResetOptions>((ids, options, bannerManager) => {
    this.addIds(this._resettingRefTestIds, ids);
    return this._dataService.resetRefTests(
      { ids, resetType: options.resetType, regenerateToken: options.regenerateToken },
      this._destroyRef,
      {
        onSuccess: ({ successCount, failedCount }) => {
          if (successCount > 0) {
            this._selectionManager.clearSelection();
            this._bannerService.success(
              this._translateService.instant('ref_tests.list.reset_success', {
                count: successCount,
              }),
            );
          }
          if (failedCount > 0) {
            bannerManager?.error(
              this._translateService.instant('ref_tests.list.reset_partial_error', {
                count: failedCount,
              }),
            );
          }
        },
        onError: () =>
          bannerManager?.error(this._translateService.instant('ref_tests.list.reset_error')),
        onComplete: () => this.removeIds(this._resettingRefTestIds, ids),
      },
    );
  }, this.getSelectedIds);

  readonly reviveDialog = createDialogOperation((ids, _, bannerManager) => {
    this.addIds(this._revivingRefTestIds, ids);
    return this._dataService.reviveRefTests(ids, this._destroyRef, {
      onSuccess: ({ successCount, failedCount }) => {
        if (successCount > 0) {
          this._selectionManager.clearSelection();
          this._bannerService.success(
            this._translateService.instant('ref_tests.list.revive_success', {
              count: successCount,
            }),
          );
        }
        if (failedCount > 0) {
          bannerManager?.error(
            this._translateService.instant('ref_tests.list.revive_partial_error', {
              count: failedCount,
            }),
          );
        }
      },
      onError: () =>
        bannerManager?.error(this._translateService.instant('ref_tests.list.revive_error')),
      onComplete: () => this.removeIds(this._revivingRefTestIds, ids),
    });
  }, this.getSelectedIds);
}
