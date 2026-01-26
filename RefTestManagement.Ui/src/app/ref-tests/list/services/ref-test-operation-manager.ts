import { inject, Injectable, signal, WritableSignal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { RefTestStatus } from '../../../../../graphql/generated';
import { Banner } from '../../../services/banner';
import { createDialogOperation } from '../../../shared/utils/apollo-utils';
import { RefTestData } from '../../services/ref-test-data';
import { REF_TEST_CONFIG } from './constants';
import { RefTestLocalStateManager } from './ref-test-local-state-manager';
import { RefTestSelectionManager } from './ref-test-selection-manager';
import { IReportResult, IResetOptions, RefTestNode } from './types';

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

  // ========================================================================
  // OPERATION STATE
  // ========================================================================
  private readonly _deletingRefTestIds = signal<Set<string>>(new Set());
  private readonly _sendingInvitationIds = signal<Set<string>>(new Set());
  private readonly _sendingResultsIds = signal<Set<string>>(new Set());
  private readonly _resettingRefTestIds = signal<Set<string>>(new Set());
  private readonly _revivingRefTestIds = signal<Set<string>>(new Set());
  // ========================================================================
  // LOADING STATE
  // ========================================================================
  readonly deletingRefTests = signal(false);
  readonly sendingInvitations = signal(false);
  readonly sendingResults = signal(false);
  readonly generatingReport = signal(false);
  readonly resettingRefTests = signal(false);
  readonly revivingRefTests = signal(false);

  // ========================================================================
  // RESULT STATE
  // ========================================================================
  readonly reportResult = signal<IReportResult | null>(null);

  // ========================================================================
  // DIALOGS
  // ========================================================================
  readonly showDeleteDialog = signal(false);
  readonly showSendInvitationsDialog = signal(false);
  readonly showSendResultsDialog = signal(false);
  readonly showGenerateReportDialog = signal(false);
  readonly showResetDialog = signal(false);
  readonly showReviveDialog = signal(false);

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

  private scheduleReportDismissal(): void {
    setTimeout(() => this.reportResult.set(null), REF_TEST_CONFIG.REPORT_BANNER_TIMEOUT_MS);
  }

  private getSelectedIds = () => Array.from(this._selectionManager.selectedIds());

  // ========================================================================
  // DIALOG OPERATIONS
  // ========================================================================

  readonly deleteDialog = createDialogOperation(
    this.showDeleteDialog,
    () => Array.from(this._selectionManager.selectedIds()),
    (ids, destroyRef) => {
      this.addIds(this._deletingRefTestIds, ids);
      this._dataService.deleteRefTests(ids, this.deletingRefTests, destroyRef, {
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
    },
  );

  readonly sendInvitationsDialog = createDialogOperation<RefTestNode[]>(
    this.showSendInvitationsDialog,
    this.getSelectedIds,
    (ids, destroyRef, allRefTests) => {
      const filtered = ids.filter(
        (id) => allRefTests.find((t) => t.id === id)?.status === RefTestStatus.Pending,
      );
      this.addIds(this._sendingInvitationIds, filtered);
      this._dataService.sendInvitations(filtered, this.sendingInvitations, destroyRef, {
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
  );

  readonly sendResultsDialog = createDialogOperation<RefTestNode[]>(
    this.showSendResultsDialog,
    this.getSelectedIds,
    (ids, destroyRef, allRefTests) => {
      const filtered = ids.filter(
        (id) => allRefTests.find((t) => t.id === id)?.status === RefTestStatus.Completed,
      );
      this.addIds(this._sendingResultsIds, filtered);
      this._dataService.sendResults(filtered, this.sendingResults, destroyRef, {
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
  );

  readonly generateReportDialog = createDialogOperation(
    this.showGenerateReportDialog,
    this.getSelectedIds,
    (ids, destroyRef) => {
      this.reportResult.set(null);
      this._dataService.generateReport(ids, this.generatingReport, destroyRef, {
        onSuccess: (result) => {
          this.reportResult.set(result);
          this.scheduleReportDismissal();
          if (result.success) {
            this._selectionManager.clearSelection();
            this._bannerService.success(
              this._translateService.instant('ref_tests.list.report_success'),
              this._translateService.instant('ref_tests.list.report.ref_test_count', {
                count: result.refTestCount,
              }),
            );
          } else {
            this._bannerService.error(
              this._translateService.instant('ref_tests.list.report_error'),
            );
          }
        },
        onError: () => {
          this.reportResult.set({ success: false, refTestCount: 0 });
          this.scheduleReportDismissal();
          this._bannerService.error(this._translateService.instant('ref_tests.list.report_error'));
        },
      });
    },
  );

  readonly resetDialog = createDialogOperation<IResetOptions>(
    this.showResetDialog,
    this.getSelectedIds,
    (ids, destroyRef, options) => {
      this.addIds(this._resettingRefTestIds, ids);
      this._dataService.resetRefTests(
        { ids, resetType: options.resetType, regenerateToken: options.regenerateToken },
        this.resettingRefTests,
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
  );

  readonly reviveDialog = createDialogOperation(
    this.showReviveDialog,
    this.getSelectedIds,
    (ids, destroyRef) => {
      this.addIds(this._revivingRefTestIds, ids);
      this._dataService.reviveRefTests(ids, this.revivingRefTests, destroyRef, {
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
    },
  );
}
