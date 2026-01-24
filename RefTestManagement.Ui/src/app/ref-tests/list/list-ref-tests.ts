import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { onlyCompleteData } from 'apollo-angular';
import { map } from 'rxjs';
import {
  GetScoreConfigurationGQL,
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
import { ColumnVisibilityManager } from './services/column-visibility-manager';
import { COLUMNS, REF_TEST_CONFIG } from './services/constants';
import { RefTestData } from './services/ref-test-data';
import { RefTestFilterState } from './services/ref-test-filter-state';
import { RefTestOperationManager } from './services/ref-test-operation-manager';
import { RefTestQueryBuilder } from './services/ref-test-query-builder';
import { RefTestSelectionManager } from './services/ref-test-selection-manager';
import { RefTestNode, SortField } from './services/types';

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
  providers: [
    RefTestFilterState,
    RefTestQueryBuilder,
    RefTestData,
    RefTestSelectionManager,
    RefTestOperationManager,
    ColumnVisibilityManager,
  ],
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
  private readonly _router = inject(Router);
  private readonly _scoreConfigGQL = inject(GetScoreConfigurationGQL);

  // Services
  protected readonly filterState = inject(RefTestFilterState);
  protected readonly queryBuilder = inject(RefTestQueryBuilder);
  protected readonly dataService = inject(RefTestData);
  protected readonly selectionManager = inject(RefTestSelectionManager);
  protected readonly operationManager = inject(RefTestOperationManager);
  protected readonly columnVisibility = inject(ColumnVisibilityManager);

  // ========================================================================
  // CONSTANTS
  // ========================================================================
  protected readonly RefTestStatus = RefTestStatus;
  protected readonly SortEnumType = SortEnumType;
  protected readonly COLUMNS = COLUMNS;
  protected readonly REF_TEST_CONFIG = REF_TEST_CONFIG;

  // ========================================================================
  // UI STATE
  // ========================================================================
  protected readonly isRefreshing = signal(false);

  // ========================================================================
  // DATA STATE
  // ========================================================================
  protected readonly loadingMore = signal(false);
  private readonly _additionalLoadedRefTests = signal<RefTestNode[]>([]);
  private readonly _deletedRefTestIds = signal<Set<string>>(new Set());
  private readonly _updatedInvitationIds = signal<Set<string>>(new Set());
  private readonly _updatedResultsIds = signal<Set<string>>(new Set());

  // ========================================================================
  // SEARCH
  // ========================================================================
  private _searchDebounceTimer: ReturnType<typeof setTimeout> | null = null;

  // ========================================================================
  // COMPUTED VALUES - Configuration
  // ========================================================================

  protected readonly passingPercentage = toSignal(
    this._scoreConfigGQL.watch().valueChanges.pipe(
      onlyCompleteData(),
      map((result) => result.data.scoreConfiguration.passingPercentage),
    ),
    { initialValue: 0 },
  );

  // ========================================================================
  // COMPUTED VALUES - Data
  // ========================================================================

  // Base computed that reads from the Apollo query result
  // All other computeds should derive from this to avoid redundant signal reads
  private readonly _queryData = computed(() => {
    return this.dataService.queryResult()?.data?.refTests;
  });

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

  protected readonly totalCount = computed(() => this._queryData()?.totalCount ?? 0);

  protected readonly loading = signal(false);
  protected readonly statusCounts = this.dataService.statusCounts;

  // Only show main loading spinner on initial load, not during pagination
  protected readonly showMainLoading = computed(() => {
    return this.loading() && !this.loadingMore() && this.refTests().length === 0;
  });

  // ========================================================================
  // COMPUTED VALUES - Selection Summaries (delegated to selection manager)
  // ========================================================================

  protected readonly invitationSummary = this.selectionManager.createInvitationSummaryComputed(() =>
    this.allLoadedRefTests(),
  );

  protected readonly resultsSummary = this.selectionManager.createResultsSummaryComputed(() =>
    this.allLoadedRefTests(),
  );

  protected readonly refTestsToDelete = this.selectionManager.createRefTestsToDeleteComputed(() =>
    this.allLoadedRefTests(),
  );

  protected readonly reportSummary = this.selectionManager.createReportSummaryComputed(() =>
    this.allLoadedRefTests(),
  );

  // ========================================================================
  // COMPUTED VALUES - Selection State (delegated to selection manager)
  // ========================================================================

  protected readonly hasCompletedRefTestsSelected =
    this.selectionManager.createHasCompletedSelectedComputed(() => this.allLoadedRefTests());

  protected readonly hasPendingRefTestsSelected =
    this.selectionManager.createHasPendingSelectedComputed(() => this.allLoadedRefTests());

  protected readonly allSelected = this.selectionManager.createAllSelectedComputed(
    () => this.refTests(),
    () => this.totalCount(),
  );

  protected readonly someSelected = this.selectionManager.createSomeSelectedComputed(
    () => this.refTests(),
    () => this.allSelected(),
  );

  protected readonly selectedCount = this.selectionManager.selectedCount;

  // ========================================================================
  // COMPUTED VALUES - Apollo Query Results
  // ========================================================================

  protected readonly allLoadedRefTests = computed((): RefTestNode[] => {
    const queryData = this._queryData();
    if (!queryData) return [];

    const edges = queryData.edges ?? [];
    const baseRefTests = edges
      .filter(
        (edge): edge is NonNullable<typeof edge> & { node: RefTestNode } => !!edge && !!edge.node,
      )
      .map((edge) => edge.node);

    // Include additional loaded tests from pagination, deduplicate by ID
    const seenIds = new Set<string>();
    const allTests: RefTestNode[] = [];

    for (const test of [...baseRefTests, ...this._additionalLoadedRefTests()]) {
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

  // ========================================================================
  // LIFECYCLE
  // ========================================================================

  constructor() {
    effect(() => {
      if (this.isRefreshing() && !this.loading()) {
        this.isRefreshing.set(false);
      }
    });
  }

  // ========================================================================
  // HELPER METHODS
  // ========================================================================

  private handleFilterChange(): void {
    this.resetPagination();
  }

  private handleSearchChange(searchTerm: string): void {
    // Clear existing timer
    if (this._searchDebounceTimer) {
      clearTimeout(this._searchDebounceTimer);
    }

    // Set new timer
    this._searchDebounceTimer = setTimeout(() => {
      this.filterState.setSearchTerm(searchTerm);
      this.handleFilterChange();
    }, REF_TEST_CONFIG.SEARCH_DEBOUNCE_MS);
  }

  // ========================================================================
  // LOAD MORE HANDLING
  // ========================================================================

  protected loadMore(): void {
    if (this.loadingMore() || !this.dataService.canLoadMore()) {
      return;
    }

    this.loadingMore.set(true);

    this.dataService
      .fetchMore()
      .then((result) => {
        if (result.data?.refTests) {
          const baseIds = new Set(
            (this._queryData()?.edges ?? [])
              .map((e) => e?.node?.id)
              .filter((id): id is string => !!id),
          );

          const edges = result.data.refTests.edges ?? [];
          const newRefTests = edges
            .filter((edge): edge is NonNullable<typeof edge> => !!edge && !!edge.node)
            .map((edge) => edge.node as RefTestNode);

          this._additionalLoadedRefTests.update((current) => {
            const alreadyLoadedIds = new Set([...baseIds, ...current.map((t) => t.id)]);
            const uniqueNew = newRefTests.filter((t) => !alreadyLoadedIds.has(t.id));
            return uniqueNew.length > 0 ? [...current, ...uniqueNew] : current;
          });
        }
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
  }

  private resetPagination(): void {
    this._additionalLoadedRefTests.set([]);
    this._deletedRefTestIds.set(new Set());
    this._updatedInvitationIds.set(new Set());
    this._updatedResultsIds.set(new Set());
  }

  // ========================================================================
  // FILTER METHODS
  // ========================================================================

  protected setStatusFilter(status?: RefTestStatus): void {
    this.filterState.setStatus(status);
    this.handleFilterChange();
  }

  protected setTitleFilter(titleId?: string): void {
    this.filterState.setTitle(titleId);
    this.handleFilterChange();
  }

  protected setInvitationFilter(invitationSent?: boolean): void {
    this.filterState.setInvitationSent(invitationSent);
    this.handleFilterChange();
  }

  protected setResultsFilter(resultsSent?: boolean): void {
    this.filterState.setResultsSent(resultsSent);
    this.handleFilterChange();
  }

  protected setSorting(sortField: SortField, sortDirection: SortEnumType): void {
    this.filterState.setSorting(sortField, sortDirection);
    this.handleFilterChange();
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
    this.handleFilterChange();
  }

  protected setDateRange(type: 'started' | 'completed', after?: string, before?: string): void {
    this.filterState.setDateRange(type, after, before);
    this.handleFilterChange();
  }

  protected sortByColumn(field: SortField): void {
    this.filterState.toggleSortDirection(field);
  }

  protected getAriaSort(field: SortField): 'none' | 'ascending' | 'descending' {
    const filter = this.filterState.filter();
    if (filter.sortField !== field) {
      return 'none';
    }

    return filter.sortDirection === SortEnumType.Asc ? 'ascending' : 'descending';
  }

  protected onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.handleSearchChange(value);
  }

  // ========================================================================
  // NAVIGATION
  // ========================================================================

  protected navigateToCreate(): void {
    this._router.navigate(['/ref-tests/create']);
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
  // SELECTION METHODS (delegated to selection manager)
  // ========================================================================

  protected toggleSelectAll(): void {
    this.selectionManager.toggleSelectAll(this.refTests());
  }

  protected toggleRefTestSelection(refTestId: string): void {
    this.selectionManager.toggleSelection(refTestId);
  }

  protected isSelected(refTestId: string): boolean {
    return this.selectionManager.isSelected(refTestId);
  }

  // ========================================================================
  // COLUMN VISIBILITY (delegated to column visibility manager)
  // ========================================================================

  protected isColumnVisible(column: string): boolean {
    return this.columnVisibility.isColumnVisible(column);
  }

  protected toggleColumn(column: string): void {
    this.columnVisibility.toggleColumn(column);
  }

  protected toggleColumnMenu(): void {
    this.columnVisibility.toggleColumnMenu();
  }

  // ========================================================================
  // DELETE OPERATIONS (delegated to operation manager)
  // ========================================================================

  protected deleteSelectedRefTests(): void {
    this.operationManager.initiateDelete();
  }

  protected confirmDelete(): void {
    this.operationManager.confirmDelete((deletedIds) => {
      // Add deleted IDs to the signal to filter them out
      this._deletedRefTestIds.update((ids) => {
        const newIds = new Set(ids);
        deletedIds.forEach((id) => newIds.add(id));
        return newIds;
      });
    });
  }

  protected cancelDelete(): void {
    this.operationManager.cancelDelete();
  }

  protected isDeleting(refTestId: string): boolean {
    return this.operationManager.isDeleting(refTestId);
  }

  // ========================================================================
  // INVITATION OPERATIONS (delegated to operation manager)
  // ========================================================================

  protected sendInvitationsToSelected(): void {
    this.operationManager.initiateSendInvitations();
  }

  protected confirmSendInvitations(): void {
    this.operationManager.confirmSendInvitations(this.allLoadedRefTests(), (sentIds) => {
      // Add sent IDs to the signal to update their state
      this._updatedInvitationIds.update((ids) => {
        const newIds = new Set(ids);
        sentIds.forEach((id) => newIds.add(id));
        return newIds;
      });
    });
  }

  protected cancelSendInvitations(): void {
    this.operationManager.cancelSendInvitations();
  }

  protected isSendingInvitation(refTestId: string): boolean {
    return this.operationManager.isSendingInvitation(refTestId);
  }

  // ========================================================================
  // RESULTS OPERATIONS (delegated to operation manager)
  // ========================================================================

  protected sendResultsToSelected(): void {
    this.operationManager.initiateSendResults();
  }

  protected confirmSendResults(): void {
    this.operationManager.confirmSendResults(this.allLoadedRefTests(), (sentIds) => {
      // Add sent IDs to the signal to update their state
      this._updatedResultsIds.update((ids) => {
        const newIds = new Set(ids);
        sentIds.forEach((id) => newIds.add(id));
        return newIds;
      });
    });
  }

  protected cancelSendResults(): void {
    this.operationManager.cancelSendResults();
  }

  // ========================================================================
  // REPORT OPERATIONS (delegated to operation manager)
  // ========================================================================

  protected generateReportForSelected(): void {
    this.operationManager.initiateGenerateReport();
  }

  protected confirmGenerateReport(): void {
    this.operationManager.confirmGenerateReport();
  }

  protected cancelGenerateReport(): void {
    this.operationManager.cancelGenerateReport();
  }

  protected dismissReportResult(): void {
    this.operationManager.dismissReportResult();
  }
}
