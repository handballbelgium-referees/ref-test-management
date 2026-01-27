import { inject, Injectable, signal, WritableSignal } from '@angular/core';
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
@Injectable({ providedIn: 'root' })
export class RefTestOperationManager {
  private readonly _bannerService = inject(Banner);
  private readonly _translateService = inject(TranslateService);
  private readonly _dataService = inject(RefTestData);
  private readonly _selectionManager = inject(RefTestSelectionManager);
  private readonly _localStateManager = inject(RefTestLocalStateManager);

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

  readonly deleteDialog = createDialogOperation((ids, destroyRef, _, loading) => {
    this.addIds(this._deletingRefTestIds, ids);
    this._dataService.deleteRefTests(ids, loading, destroyRef, {
      onSuccess: (deletedIds) => {
        this._localStateManager.markAsDeleted(deletedIds);
        this._selectionManager.clearSelection();
        this._bannerService.success(
          this._translateService.instant('ref_tests.list.delete_success'),
        );
      },
      onError: () =>
        this._bannerService.error(this._translateService.instant('ref_tests.list.delete_error')),
      onComplete: () => this.removeIds(this._deletingRefTestIds, ids),
    });
  }, this.getSelectedIds);

  readonly sendInvitationsDialog = createDialogOperation<RefTestNode[]>(
    (ids, destroyRef, allRefTests, loading) => {
      const filtered = ids.filter(
        (id) => allRefTests.find((t) => t.id === id)?.status === RefTestStatus.Pending,
      );
      this.addIds(this._sendingInvitationIds, filtered);
      this._dataService.sendInvitations(filtered, loading, destroyRef, {
        onSuccess: (sendIds) => {
          this._localStateManager.markInvitationsSent(sendIds);
          this._selectionManager.clearSelection();
          this._bannerService.success(
            this._translateService.instant('ref_tests.list.invitations_sent'),
          );
        },
        onError: () =>
          this._bannerService.error(
            this._translateService.instant('ref_tests.list.invitations_error'),
          ),
        onComplete: () => this.removeIds(this._sendingInvitationIds, filtered),
      });
    },
    this.getSelectedIds,
  );

  readonly sendResultsDialog = createDialogOperation<RefTestNode[]>(
    (ids, destroyRef, allRefTests, loading) => {
      const filtered = ids.filter(
        (id) => allRefTests.find((t) => t.id === id)?.status === RefTestStatus.Completed,
      );
      this.addIds(this._sendingResultsIds, filtered);
      this._dataService.sendResults(filtered, loading, destroyRef, {
        onSuccess: () => {
          this._selectionManager.clearSelection();
          this._bannerService.success(
            this._translateService.instant('ref_tests.list.results_sent'),
          );
        },
        onError: () =>
          this._bannerService.error(this._translateService.instant('ref_tests.list.results_error')),
        onComplete: () => this.removeIds(this._sendingResultsIds, filtered),
      });
    },
    this.getSelectedIds,
  );

  readonly generateReportDialog = createDialogOperation((ids, destroyRef, _, loading) => {
    this._dataService.generateReport(ids, loading, destroyRef, {
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
          this._bannerService.error(this._translateService.instant('ref_tests.list.report_error'));
        }
      },
      onError: () => {
        this._bannerService.error(this._translateService.instant('ref_tests.list.report_error'));
      },
    });
  }, this.getSelectedIds);

  readonly resetDialog = createDialogOperation<IResetOptions>(
    (ids, destroyRef, options, loading) => {
      this.addIds(this._resettingRefTestIds, ids);
      this._dataService.resetRefTests(
        { ids, resetType: options.resetType, regenerateToken: options.regenerateToken },
        loading,
        destroyRef,
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
              this._bannerService.error(
                this._translateService.instant('ref_tests.list.reset_partial_error', {
                  count: failedCount,
                }),
              );
            }
          },
          onError: () =>
            this._bannerService.error(this._translateService.instant('ref_tests.list.reset_error')),
          onComplete: () => this.removeIds(this._resettingRefTestIds, ids),
        },
      );
    },
    this.getSelectedIds,
  );

  readonly reviveDialog = createDialogOperation((ids, destroyRef, _, loading) => {
    this.addIds(this._revivingRefTestIds, ids);
    this._dataService.reviveRefTests(ids, loading, destroyRef, {
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
          this._bannerService.error(
            this._translateService.instant('ref_tests.list.revive_partial_error', {
              count: failedCount,
            }),
          );
        }
      },
      onError: () =>
        this._bannerService.error(this._translateService.instant('ref_tests.list.revive_error')),
      onComplete: () => this.removeIds(this._revivingRefTestIds, ids),
    });
  }, this.getSelectedIds);
}
