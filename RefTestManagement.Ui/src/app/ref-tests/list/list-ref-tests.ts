import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  HostListener,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { onlyCompleteData } from 'apollo-angular';
import { debounceTime, map, Subject } from 'rxjs';
import {
  GetScoreConfigurationGQL,
  RefTestsEdge,
  RefTestStatus,
  SortEnumType,
} from '../../../../graphql/generated';
import { PullToRefresh } from '../../shared/components/pull-to-refresh/pull-to-refresh';
import { ColumnVisibilityMenu } from './components/column-visibility-menu/column-visibility-menu';
import { DeleteRefTestsDialog } from './components/dialogs/delete-ref-tests-dialog/delete-ref-tests-dialog';
import { GenerateReportDialog } from './components/dialogs/generate-report-dialog/generate-report-dialog';
import { SendInvitationsDialog } from './components/dialogs/send-invitations-dialog/send-invitations-dialog';
import { SendResultsDialog } from './components/dialogs/send-results-dialog/send-results-dialog';
import { RefTestFiltersCard } from './components/filters/ref-test-filters-card/ref-test-filters-card';
import { RefTestBulkActions } from './components/ref-test-bulk-actions/ref-test-bulk-actions';
import { RefTestMobileCard } from './components/ref-test-display/ref-test-mobile-card/ref-test-mobile-card';
import { RefTestTableRow } from './components/ref-test-display/ref-test-table-row/ref-test-table-row';
import { COLUMNS, REF_TEST_CONFIG } from './services/constants';
import { RefTestData } from './services/ref-test-data';
import { RefTestFilterState } from './services/ref-test-filter-state';
import { RefTestQueryBuilder } from './services/ref-test-query-builder';
import { IParticipantInfo, IReportResult, RefTestNode, SortField } from './services/types';

/**
 * Main component for listing and managing reference tests.
 * Provides filtering, sorting, selection, and bulk operations.
 */
@Component({
  selector: 'app-list-ref-tests',
  imports: [
    TranslatePipe,
    RefTestFiltersCard,
    RefTestBulkActions,
    RefTestTableRow,
    RefTestMobileCard,
    ColumnVisibilityMenu,
    SendInvitationsDialog,
    SendResultsDialog,
    DeleteRefTestsDialog,
    GenerateReportDialog,
    PullToRefresh,
  ],
  providers: [RefTestFilterState, RefTestQueryBuilder, RefTestData],
  templateUrl: './list-ref-tests.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class ListRefTests {
  // ========================================================================
  // DEPENDENCIES
  // ========================================================================
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly scoreConfigGQL = inject(GetScoreConfigurationGQL);

  // Services
  protected readonly filterState = inject(RefTestFilterState);
  protected readonly queryBuilder = inject(RefTestQueryBuilder);
  protected readonly dataService = inject(RefTestData);

  // ========================================================================
  // CONSTANTS
  // ========================================================================
  protected readonly RefTestStatus = RefTestStatus;
  protected readonly SortEnumType = SortEnumType;
  protected readonly COLUMNS = COLUMNS;

  // ========================================================================
  // UI STATE
  // ========================================================================
  protected readonly visibleColumns = signal(
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
  protected readonly showColumnMenu = signal(false);
  protected readonly isRefreshing = signal(false);

  // ========================================================================
  // DATA STATE
  // ========================================================================
  protected readonly allLoadedRefTests = signal<RefTestNode[]>([]);
  protected readonly loadingMore = signal(false);
  private readonly endCursor = signal<string | undefined>(undefined);
  protected readonly hasNextPage = signal(false);

  // ========================================================================
  // SELECTION STATE
  // ========================================================================
  protected readonly selectedRefTestIds = signal<Set<string>>(new Set());

  // ========================================================================
  // OPERATION STATE
  // ========================================================================
  protected readonly deletingRefTestIds = signal<Set<string>>(new Set());
  protected readonly sendingInvitationIds = signal<Set<string>>(new Set());
  protected readonly sendingResultsIds = signal<Set<string>>(new Set());

  // ========================================================================
  // DIALOG STATE
  // ========================================================================
  protected readonly showSendInvitationsDialog = signal(false);
  protected readonly showSendResultsDialog = signal(false);
  protected readonly showDeleteDialog = signal(false);
  protected readonly showGenerateReportDialog = signal(false);

  // ========================================================================
  // LOADING STATE
  // ========================================================================
  protected readonly sendingInvitations = signal(false);
  protected readonly sendingResults = signal(false);
  protected readonly deletingRefTests = signal(false);
  protected readonly generatingReport = signal(false);

  // ========================================================================
  // RESULT STATE
  // ========================================================================
  protected readonly reportResult = signal<IReportResult | null>(null);

  // ========================================================================
  // SEARCH
  // ========================================================================
  private readonly searchSubject = new Subject<string>();

  // ========================================================================
  // COMPUTED VALUES - Configuration
  // ========================================================================

  protected readonly passingPercentage = toSignal(
    this.scoreConfigGQL.watch().valueChanges.pipe(
      onlyCompleteData(),
      map((result) => result.data.scoreConfiguration.passingPercentage),
    ),
    { initialValue: 0 },
  );

  // ========================================================================
  // COMPUTED VALUES - Data
  // ========================================================================

  protected readonly refTests = computed((): RefTestNode[] => {
    const allTests = this.allLoadedRefTests();
    const searchTerm = this.filterState.filter().searchTerm.toLowerCase();

    if (!searchTerm) {
      return allTests;
    }

    return allTests.filter(
      (test) =>
        test.name?.toLowerCase().includes(searchTerm) ||
        test.email?.toLowerCase().includes(searchTerm),
    );
  });

  protected readonly totalCount = computed(
    () => this.dataService.queryResult()?.data?.refTests?.totalCount ?? 0,
  );

  protected readonly loading = this.dataService.loading;
  protected readonly statusCounts = this.dataService.statusCounts;

  // ========================================================================
  // COMPUTED VALUES - Selection Summaries
  // ========================================================================

  protected readonly invitationSummary = computed(() => {
    const selectedIds = this.selectedRefTestIds();
    const allRefTests = this.allLoadedRefTests();
    const pendingSelected = allRefTests.filter(
      (t) => selectedIds.has(t.id) && t.status === RefTestStatus.Pending,
    );

    return {
      newInvitations: this.mapToParticipantInfo(pendingSelected.filter((t) => !t.invitationSent)),
      resendInvitations: this.mapToParticipantInfo(pendingSelected.filter((t) => t.invitationSent)),
    };
  });

  protected readonly resultsSummary = computed(() => {
    const selectedIds = this.selectedRefTestIds();
    const allRefTests = this.allLoadedRefTests();
    const completedSelected = allRefTests.filter(
      (t) => selectedIds.has(t.id) && t.status === RefTestStatus.Completed,
    );

    return {
      newResults: this.mapToParticipantInfo(completedSelected.filter((t) => !t.resultsSent)),
      resendResults: this.mapToParticipantInfo(completedSelected.filter((t) => t.resultsSent)),
    };
  });

  protected readonly refTestsToDelete = computed(() => {
    const selectedIds = this.selectedRefTestIds();
    const allRefTests = this.allLoadedRefTests();
    return this.mapToParticipantInfo(allRefTests.filter((t) => selectedIds.has(t.id)));
  });

  protected readonly reportSummary = computed(() => {
    const selectedIds = this.selectedRefTestIds();
    const allRefTests = this.allLoadedRefTests();
    return {
      refTests: this.mapToParticipantInfo(allRefTests.filter((t) => selectedIds.has(t.id))),
    };
  });

  // ========================================================================
  // COMPUTED VALUES - Selection State
  // ========================================================================

  protected readonly hasCompletedRefTestsSelected = computed(() => {
    const selectedIds = this.selectedRefTestIds();
    const allRefTests = this.allLoadedRefTests();
    return allRefTests.some((t) => selectedIds.has(t.id) && t.status === RefTestStatus.Completed);
  });

  protected readonly hasPendingRefTestsSelected = computed(() => {
    const selectedIds = this.selectedRefTestIds();
    const allRefTests = this.allLoadedRefTests();
    return allRefTests.some((t) => selectedIds.has(t.id) && t.status === RefTestStatus.Pending);
  });

  protected readonly allSelected = computed(() => {
    const refTests = this.refTests();
    const selected = this.selectedRefTestIds();
    const totalCount = this.totalCount();

    if (selected.size > 0 && selected.size === totalCount) {
      return true;
    }

    return refTests.length > 0 && refTests.every((t) => selected.has(t.id));
  });

  protected readonly someSelected = computed(() => {
    const refTests = this.refTests();
    const selected = this.selectedRefTestIds();
    return refTests.some((t) => selected.has(t.id)) && !this.allSelected();
  });

  protected readonly selectedCount = computed(() => this.selectedRefTestIds().size);

  // ========================================================================
  // LIFECYCLE
  // ========================================================================

  constructor() {
    this.setupSearchDebounce();
    this.setupRefreshSync();
    this.setupQueryResultsHandler();
    this.setupFilterChangeHandler();
  }

  // ========================================================================
  // SETUP METHODS
  // ========================================================================

  private setupSearchDebounce(): void {
    this.searchSubject
      .pipe(debounceTime(REF_TEST_CONFIG.SEARCH_DEBOUNCE_MS), takeUntilDestroyed(this.destroyRef))
      .subscribe((searchTerm) => {
        this.filterState.setSearchTerm(searchTerm);
      });
  }

  private setupRefreshSync(): void {
    effect(() => {
      const isLoading = this.loading();
      if (!isLoading && this.isRefreshing()) {
        this.isRefreshing.set(false);
      }
    });
  }

  private setupQueryResultsHandler(): void {
    effect(() => {
      const result = this.dataService.queryResult();
      if (!result?.data?.refTests) return;

      const edges = result.data.refTests.edges ?? [];
      const newRefTests = edges
        .filter(
          (edge): edge is NonNullable<typeof edge> & { node: RefTestNode } => !!edge && !!edge.node,
        )
        .map((edge) => edge.node);

      this.allLoadedRefTests.set(newRefTests);
      this.hasNextPage.set(result.data.refTests.pageInfo?.hasNextPage ?? false);
      this.endCursor.set(result.data.refTests.pageInfo?.endCursor ?? undefined);
    });
  }

  private setupFilterChangeHandler(): void {
    effect(() => {
      this.filterState.filter(); // Track changes
      this.resetPagination();
      this.refetchData();
      this.dataService.updateCountQueries();
    });
  }

  // ========================================================================
  // SCROLL HANDLING
  // ========================================================================

  @HostListener('window:scroll')
  onScroll(): void {
    if (this.loadingMore() || !this.hasNextPage()) {
      return;
    }

    const scrollPosition = window.innerHeight + window.scrollY;
    const documentHeight = document.documentElement.scrollHeight;

    if (scrollPosition >= documentHeight - REF_TEST_CONFIG.SCROLL_THRESHOLD) {
      this.loadMore();
    }
  }

  protected loadMore(): void {
    if (this.loadingMore() || !this.hasNextPage()) {
      return;
    }

    this.loadingMore.set(true);

    const filter = this.filterState.filter();
    const where = this.queryBuilder.buildWhereFilter(filter);
    const order = this.queryBuilder.buildOrderClause(filter);

    this.dataService
      .fetchMore({
        first: REF_TEST_CONFIG.PAGE_SIZE,
        after: this.endCursor(),
        where,
        order,
      })
      .then((result) => {
        if (result.data?.refTests) {
          const edges = result.data.refTests.edges ?? [];
          const newRefTests = edges
            .filter(
              (edge: RefTestsEdge): edge is NonNullable<typeof edge> & { node: RefTestNode } =>
                !!edge && !!edge.node,
            )
            .map((edge: RefTestsEdge) => edge.node);

          this.allLoadedRefTests.update((current) => [...current, ...newRefTests]);
          this.hasNextPage.set(result.data.refTests.pageInfo?.hasNextPage ?? false);
          this.endCursor.set(result.data.refTests.pageInfo?.endCursor ?? undefined);
        }
      })
      .catch((error) => {
        console.error('Error loading more ref tests:', error);
      })
      .finally(() => {
        this.loadingMore.set(false);
      });
  }

  // ========================================================================
  // DATA OPERATIONS
  // ========================================================================

  protected refresh(): void {
    if (this.isRefreshing()) {
      return;
    }

    this.isRefreshing.set(true);
    this.resetPagination();
    this.refetchData();
    this.dataService.updateCountQueries();
  }

  private resetPagination(): void {
    this.allLoadedRefTests.set([]);
    this.endCursor.set(undefined);
  }

  private refetchData(): void {
    const filter = this.filterState.filter();
    const where = this.queryBuilder.buildWhereFilter(filter);
    const order = this.queryBuilder.buildOrderClause(filter);

    this.dataService.refetchRefTests({
      first: REF_TEST_CONFIG.PAGE_SIZE,
      after: undefined,
      where,
      order,
    });
  }

  // ========================================================================
  // FILTER METHODS
  // ========================================================================

  protected setStatusFilter(status?: RefTestStatus): void {
    this.filterState.setStatus(status);
  }

  protected setTitleFilter(titleId?: string): void {
    this.filterState.setTitle(titleId);
  }

  protected setInvitationFilter(invitationSent?: boolean): void {
    this.filterState.setInvitationSent(invitationSent);
  }

  protected setResultsFilter(resultsSent?: boolean): void {
    this.filterState.setResultsSent(resultsSent);
  }

  protected setSorting(sortField: SortField, sortDirection: SortEnumType): void {
    this.filterState.setSorting(sortField, sortDirection);
  }

  protected setPerformanceFilters(performance: {
    minQuestionScore?: number;
    maxQuestionScore?: number;
    minAnswerScore?: number;
    maxAnswerScore?: number;
    percentageRange?: 'low' | 'medium' | 'high';
    minQuestions?: number;
    maxQuestions?: number;
    minMaxTimeInMinutes?: number;
    maxMaxTimeInMinutes?: number;
  }): void {
    this.filterState.setPerformanceFilters(performance);
  }

  protected setDateRange(type: 'started' | 'completed', after?: string, before?: string): void {
    this.filterState.setDateRange(type, after, before);
  }

  protected sortByColumn(field: SortField): void {
    this.filterState.toggleSortDirection(field);
  }

  protected onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchSubject.next(value);
  }

  // ========================================================================
  // NAVIGATION
  // ========================================================================

  protected navigateToCreate(): void {
    this.router.navigate(['/ref-tests/create']);
  }

  // ========================================================================
  // UI HELPERS
  // ========================================================================

  protected getStatusClass(status: RefTestStatus): string {
    const statusClasses: Record<RefTestStatus, string> = {
      [RefTestStatus.Pending]: 'bg-yellow-100 text-yellow-800',
      [RefTestStatus.InProgress]: 'bg-blue-100 text-blue-800',
      [RefTestStatus.Completed]: 'bg-success-100 text-success-800',
      [RefTestStatus.Expired]: 'bg-red-100 text-red-800',
    };

    return statusClasses[status] ?? 'bg-neutral-100 text-neutral-800';
  }

  // ========================================================================
  // SELECTION METHODS
  // ========================================================================

  protected toggleSelectAll(): void {
    if (this.allSelected()) {
      this.selectedRefTestIds.set(new Set());
    } else {
      const allIds = this.refTests().map((t) => t.id);
      this.selectedRefTestIds.set(new Set(allIds));
    }
  }

  protected toggleRefTestSelection(refTestId: string): void {
    this.selectedRefTestIds.update((ids) => {
      const newIds = new Set(ids);
      if (newIds.has(refTestId)) {
        newIds.delete(refTestId);
      } else {
        newIds.add(refTestId);
      }
      return newIds;
    });
  }

  protected isSelected(refTestId: string): boolean {
    return this.selectedRefTestIds().has(refTestId);
  }

  // ========================================================================
  // COLUMN VISIBILITY
  // ========================================================================

  protected isColumnVisible(column: string): boolean {
    return this.visibleColumns().has(column);
  }

  protected toggleColumn(column: string): void {
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

  protected toggleColumnMenu(): void {
    this.showColumnMenu.update((show) => !show);
  }

  // ========================================================================
  // DELETE OPERATIONS
  // ========================================================================

  protected deleteSelectedRefTests(): void {
    const refTestIds = Array.from(this.selectedRefTestIds());
    if (refTestIds.length === 0) {
      return;
    }

    this.showDeleteDialog.set(true);
  }

  protected confirmDelete(): void {
    this.showDeleteDialog.set(false);
    const refTestIds = Array.from(this.selectedRefTestIds());

    // Mark as deleting
    refTestIds.forEach((id) => {
      this.deletingRefTestIds.update((ids) => new Set(ids).add(id));
    });

    this.dataService.deleteRefTests(refTestIds, {
      onStart: () => this.deletingRefTests.set(true),
      onSuccess: (deletedIds) => {
        const deletedSet = new Set(deletedIds);
        this.allLoadedRefTests.update((tests) => tests.filter((t) => !deletedSet.has(t.id)));
        this.selectedRefTestIds.set(new Set());
        this.dataService.updateCountQueries();
      },
      onError: (error) => {
        console.error('Error deleting ref tests:', error);
      },
      onComplete: () => {
        refTestIds.forEach((id) => {
          this.deletingRefTestIds.update((ids) => {
            const newIds = new Set(ids);
            newIds.delete(id);
            return newIds;
          });
        });
        this.deletingRefTests.set(false);
      },
    });
  }

  protected cancelDelete(): void {
    this.showDeleteDialog.set(false);
  }

  protected isDeleting(refTestId: string): boolean {
    return this.deletingRefTestIds().has(refTestId);
  }

  // ========================================================================
  // INVITATION OPERATIONS
  // ========================================================================

  protected sendInvitationsToSelected(): void {
    const refTestIds = Array.from(this.selectedRefTestIds());
    if (refTestIds.length === 0) {
      return;
    }

    this.showSendInvitationsDialog.set(true);
  }

  protected confirmSendInvitations(): void {
    this.showSendInvitationsDialog.set(false);
    const selectedIds = Array.from(this.selectedRefTestIds());
    const allRefTests = this.allLoadedRefTests();

    // Filter to only pending ref tests
    const refTestIds = selectedIds.filter((id) => {
      const refTest = allRefTests.find((t) => t.id === id);
      return refTest?.status === RefTestStatus.Pending;
    });

    // Mark as sending
    refTestIds.forEach((id) => {
      this.sendingInvitationIds.update((ids) => new Set(ids).add(id));
    });

    this.dataService.sendInvitations(refTestIds, {
      onStart: () => this.sendingInvitations.set(true),
      onSuccess: (sentIds) => {
        const sentSet = new Set(sentIds);
        this.allLoadedRefTests.update((tests) =>
          tests.map((t) => (sentSet.has(t.id) ? { ...t, invitationSent: true } : t)),
        );
        this.selectedRefTestIds.set(new Set());
      },
      onError: (error) => {
        console.error('Error sending invitations:', error);
      },
      onComplete: () => {
        refTestIds.forEach((id) => {
          this.sendingInvitationIds.update((ids) => {
            const newIds = new Set(ids);
            newIds.delete(id);
            return newIds;
          });
        });
        this.sendingInvitations.set(false);
      },
    });
  }

  protected cancelSendInvitations(): void {
    this.showSendInvitationsDialog.set(false);
  }

  protected isSendingInvitation(refTestId: string): boolean {
    return this.sendingInvitationIds().has(refTestId);
  }

  // ========================================================================
  // RESULTS OPERATIONS
  // ========================================================================

  protected sendResultsToSelected(): void {
    const selectedIds = Array.from(this.selectedRefTestIds());
    if (selectedIds.length === 0) {
      return;
    }

    this.showSendResultsDialog.set(true);
  }

  protected confirmSendResults(): void {
    this.showSendResultsDialog.set(false);
    const selectedIds = Array.from(this.selectedRefTestIds());
    const allRefTests = this.allLoadedRefTests();

    // Filter to only completed ref tests
    const refTestIds = selectedIds.filter((id) => {
      const refTest = allRefTests.find((t) => t.id === id);
      return refTest?.status === RefTestStatus.Completed;
    });

    // Mark as sending
    refTestIds.forEach((id) => {
      this.sendingResultsIds.update((ids) => new Set(ids).add(id));
    });

    this.dataService.sendResults(refTestIds, {
      onStart: () => this.sendingResults.set(true),
      onSuccess: (sentIds) => {
        const sentSet = new Set(sentIds);
        this.allLoadedRefTests.update((tests) =>
          tests.map((t) => (sentSet.has(t.id) ? { ...t, resultsSent: true } : t)),
        );
        this.selectedRefTestIds.set(new Set());
      },
      onError: (error) => {
        console.error('Error sending results:', error);
      },
      onComplete: () => {
        refTestIds.forEach((id) => {
          this.sendingResultsIds.update((ids) => {
            const newIds = new Set(ids);
            newIds.delete(id);
            return newIds;
          });
        });
        this.sendingResults.set(false);
      },
    });
  }

  protected cancelSendResults(): void {
    this.showSendResultsDialog.set(false);
  }

  // ========================================================================
  // REPORT OPERATIONS
  // ========================================================================

  protected generateReportForSelected(): void {
    const refTestIds = Array.from(this.selectedRefTestIds());
    if (refTestIds.length === 0) {
      return;
    }

    this.showGenerateReportDialog.set(true);
  }

  protected confirmGenerateReport(): void {
    this.showGenerateReportDialog.set(false);
    const refTestIds = Array.from(this.selectedRefTestIds());
    this.reportResult.set(null);

    this.dataService.generateReport(refTestIds, {
      onStart: () => this.generatingReport.set(true),
      onSuccess: (result) => {
        this.reportResult.set(result);
        this.scheduleReportDismissal();
        if (result.success) {
          this.selectedRefTestIds.set(new Set());
        }
      },
      onError: (error) => {
        console.error('Error generating report:', error);
        this.reportResult.set({ success: false, refTestCount: 0 });
        this.scheduleReportDismissal();
      },
      onComplete: () => {
        this.generatingReport.set(false);
      },
    });
  }

  protected cancelGenerateReport(): void {
    this.showGenerateReportDialog.set(false);
  }

  protected dismissReportResult(): void {
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

  private mapToParticipantInfo(refTests: RefTestNode[]): IParticipantInfo[] {
    return refTests.map((t) => ({
      name: t.name || '',
      email: t.email || '',
    }));
  }
}
