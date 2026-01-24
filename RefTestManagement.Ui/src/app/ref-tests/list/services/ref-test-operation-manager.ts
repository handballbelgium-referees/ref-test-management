import { inject, Injectable, signal, WritableSignal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { RefTestStatus } from '../../../../../graphql/generated';
import { Toast } from '../../../services/toast';
import { REF_TEST_CONFIG } from './constants';
import { RefTestData } from './ref-test-data';
import { RefTestSelectionManager } from './ref-test-selection-manager';
import { IReportResult, RefTestNode } from './types';

/**
 * Service responsible for managing bulk operations on ref tests
 * (delete, send invitations, send results, generate report)
 */
@Injectable()
export class RefTestOperationManager {
  private readonly _toastService = inject(Toast);
  private readonly _translateService = inject(TranslateService);
  private readonly _dataService = inject(RefTestData);
  private readonly _selectionManager = inject(RefTestSelectionManager);

  // ========================================================================
  // OPERATION STATE
  // ========================================================================

  readonly deletingRefTestIds = signal<Set<string>>(new Set());
  readonly sendingInvitationIds = signal<Set<string>>(new Set());
  readonly sendingResultsIds = signal<Set<string>>(new Set());

  // ========================================================================
  // LOADING STATE
  // ========================================================================

  readonly sendingInvitations = signal(false);
  readonly sendingResults = signal(false);
  readonly deletingRefTests = signal(false);
  readonly generatingReport = signal(false);

  // ========================================================================
  // RESULT STATE
  // ========================================================================

  readonly reportResult = signal<IReportResult | null>(null);

  // ========================================================================
  // DIALOG STATE
  // ========================================================================

  readonly showSendInvitationsDialog = signal(false);
  readonly showSendResultsDialog = signal(false);
  readonly showDeleteDialog = signal(false);
  readonly showGenerateReportDialog = signal(false);

  // ========================================================================
  // OPERATION STATUS CHECKS
  // ========================================================================

  isDeleting(refTestId: string): boolean {
    return this.deletingRefTestIds().has(refTestId);
  }

  isSendingInvitation(refTestId: string): boolean {
    return this.sendingInvitationIds().has(refTestId);
  }

  isSendingResults(refTestId: string): boolean {
    return this.sendingResultsIds().has(refTestId);
  }

  // ========================================================================
  // DELETE OPERATIONS
  // ========================================================================

  initiateDelete(): void {
    const selectedIds = Array.from(this._selectionManager.selectedIds());
    if (selectedIds.length === 0) {
      return;
    }
    this.showDeleteDialog.set(true);
  }

  confirmDelete(onDeleteSuccess: (deletedIds: string[]) => void): void {
    this.showDeleteDialog.set(false);
    const refTestIds = Array.from(this._selectionManager.selectedIds());

    // Mark as deleting
    this.addIds(this.deletingRefTestIds, refTestIds);

    this._dataService.deleteRefTests(refTestIds, {
      onStart: () => this.deletingRefTests.set(true),
      onSuccess: (deletedIds) => {
        onDeleteSuccess(deletedIds);
        this._selectionManager.clearSelection();
        this._toastService.success(this._translateService.instant('ref_tests.list.delete_success'));
      },
      onError: () => {
        this._toastService.error(this._translateService.instant('ref_tests.list.delete_error'));
      },
      onComplete: () => {
        this.removeIds(this.deletingRefTestIds, refTestIds);
        this.deletingRefTests.set(false);
      },
    });
  }

  cancelDelete(): void {
    this.showDeleteDialog.set(false);
  }

  // ========================================================================
  // INVITATION OPERATIONS
  // ========================================================================

  initiateSendInvitations(): void {
    const selectedIds = Array.from(this._selectionManager.selectedIds());
    if (selectedIds.length === 0) {
      return;
    }
    this.showSendInvitationsDialog.set(true);
  }

  confirmSendInvitations(
    allRefTests: RefTestNode[],
    onInvitationsSent: (sentIds: string[]) => void,
  ): void {
    this.showSendInvitationsDialog.set(false);
    const selectedIds = Array.from(this._selectionManager.selectedIds());

    // Filter to only pending ref tests
    const refTestIds = selectedIds.filter((id) => {
      const refTest = allRefTests.find((t) => t.id === id);
      return refTest?.status === RefTestStatus.Pending;
    });

    // Mark as sending
    this.addIds(this.sendingInvitationIds, refTestIds);

    this._dataService.sendInvitations(refTestIds, {
      onStart: () => this.sendingInvitations.set(true),
      onSuccess: (sentIds) => {
        onInvitationsSent(sentIds);
        this._selectionManager.clearSelection();
        this._toastService.success(
          this._translateService.instant('ref_tests.list.invitations_sent'),
        );
      },
      onError: () => {
        this._toastService.error(
          this._translateService.instant('ref_tests.list.invitations_error'),
        );
      },
      onComplete: () => {
        this.removeIds(this.sendingInvitationIds, refTestIds);
        this.sendingInvitations.set(false);
      },
    });
  }

  cancelSendInvitations(): void {
    this.showSendInvitationsDialog.set(false);
  }

  // ========================================================================
  // RESULTS OPERATIONS
  // ========================================================================

  initiateSendResults(): void {
    const selectedIds = Array.from(this._selectionManager.selectedIds());
    if (selectedIds.length === 0) {
      return;
    }
    this.showSendResultsDialog.set(true);
  }

  confirmSendResults(allRefTests: RefTestNode[], onResultsSent: (sentIds: string[]) => void): void {
    this.showSendResultsDialog.set(false);
    const selectedIds = Array.from(this._selectionManager.selectedIds());

    // Filter to only completed ref tests
    const refTestIds = selectedIds.filter((id) => {
      const refTest = allRefTests.find((t) => t.id === id);
      return refTest?.status === RefTestStatus.Completed;
    });

    // Mark as sending
    this.addIds(this.sendingResultsIds, refTestIds);

    this._dataService.sendResults(refTestIds, {
      onStart: () => this.sendingResults.set(true),
      onSuccess: (sentIds) => {
        onResultsSent(sentIds);
        this._selectionManager.clearSelection();
        this._toastService.success(this._translateService.instant('ref_tests.list.results_sent'));
      },
      onError: () => {
        this._toastService.error(this._translateService.instant('ref_tests.list.results_error'));
      },
      onComplete: () => {
        this.removeIds(this.sendingResultsIds, refTestIds);
        this.sendingResults.set(false);
      },
    });
  }

  cancelSendResults(): void {
    this.showSendResultsDialog.set(false);
  }

  // ========================================================================
  // REPORT OPERATIONS
  // ========================================================================

  initiateGenerateReport(): void {
    const selectedIds = Array.from(this._selectionManager.selectedIds());
    if (selectedIds.length === 0) {
      return;
    }
    this.showGenerateReportDialog.set(true);
  }

  confirmGenerateReport(): void {
    this.showGenerateReportDialog.set(false);
    const refTestIds = Array.from(this._selectionManager.selectedIds());
    this.reportResult.set(null);

    this._dataService.generateReport(refTestIds, {
      onStart: () => this.generatingReport.set(true),
      onSuccess: (result) => {
        this.reportResult.set(result);
        this.scheduleReportDismissal();
        if (result.success) {
          this._selectionManager.clearSelection();
          this._toastService.success(
            this._translateService.instant('ref_tests.list.report_success'),
          );
        } else {
          this._toastService.error(this._translateService.instant('ref_tests.list.report_error'));
        }
      },
      onError: () => {
        this.reportResult.set({ success: false, refTestCount: 0 });
        this.scheduleReportDismissal();
        this._toastService.error(this._translateService.instant('ref_tests.list.report_error'));
      },
      onComplete: () => {
        this.generatingReport.set(false);
      },
    });
  }

  cancelGenerateReport(): void {
    this.showGenerateReportDialog.set(false);
  }

  dismissReportResult(): void {
    this.reportResult.set(null);
  }

  private scheduleReportDismissal(): void {
    setTimeout(() => {
      this.dismissReportResult();
    }, REF_TEST_CONFIG.REPORT_BANNER_TIMEOUT_MS);
  }

  // ========================================================================
  // HELPER METHODS
  // ========================================================================

  private addIds(target: WritableSignal<Set<string>>, ids: string[]): void {
    target.update((current) => new Set([...current, ...ids]));
  }

  private removeIds(target: WritableSignal<Set<string>>, ids: string[]): void {
    target.update((current) => {
      const next = new Set(current);
      ids.forEach((id) => next.delete(id));
      return next;
    });
  }
}
