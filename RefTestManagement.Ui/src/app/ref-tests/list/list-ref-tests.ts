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
import { DeleteRefTestsDialog } from './components/dialogs/delete-ref-tests-dialog/delete-ref-tests-dialog';
import { GenerateReportDialog } from './components/dialogs/generate-report-dialog/generate-report-dialog';
import { SendInvitationsDialog } from './components/dialogs/send-invitations-dialog/send-invitations-dialog';
import { SendResultsDialog } from './components/dialogs/send-results-dialog/send-results-dialog';
import { RefTestFiltersCard } from './components/filters/ref-test-filters-card/ref-test-filters-card';
import { RefTestBulkActions } from './components/ref-test-bulk-actions/ref-test-bulk-actions';
import { RefTestEmptyState } from './components/ref-test-empty-state/ref-test-empty-state';
import { RefTestListHero } from './components/ref-test-list-hero/ref-test-list-hero';
import { RefTestListToolbar } from './components/ref-test-list-toolbar/ref-test-list-toolbar';
import { RefTestMobileList } from './components/ref-test-mobile-list/ref-test-mobile-list';
import { RefTestPagination } from './components/ref-test-pagination/ref-test-pagination';
import { RefTestPerformanceWarning } from './components/ref-test-performance-warning/ref-test-performance-warning';
import { RefTestReportBanner } from './components/ref-test-report-banner/ref-test-report-banner';
import { RefTestTable } from './components/ref-test-table/ref-test-table';
import { ColumnVisibilityManager } from './services/column-visibility-manager';
import { COLUMNS, REF_TEST_CONFIG } from './services/constants';
import { RefTestData } from './services/ref-test-data';
import { RefTestFilterActions } from './services/ref-test-filter-actions';
import { RefTestFilterState } from './services/ref-test-filter-state';
import { RefTestLocalStateManager } from './services/ref-test-local-state-manager';
import { RefTestOperationManager } from './services/ref-test-operation-manager';
import { RefTestQueryBuilder } from './services/ref-test-query-builder';
import { RefTestSelectionManager } from './services/ref-test-selection-manager';
import { RefTestUIHelpers } from './services/ref-test-ui-helpers';
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
    SendInvitationsDialog,
    SendResultsDialog,
    DeleteRefTestsDialog,
    GenerateReportDialog,
    PullToRefresh,
    RefTestListHero,
    RefTestListToolbar,
    RefTestReportBanner,
    RefTestEmptyState,
    RefTestTable,
    RefTestMobileList,
    RefTestPerformanceWarning,
    RefTestPagination,
  ],
  providers: [
    RefTestFilterState,
    RefTestFilterActions,
    RefTestQueryBuilder,
    RefTestData,
    RefTestSelectionManager,
    RefTestOperationManager,
    RefTestLocalStateManager,
    ColumnVisibilityManager,
    RefTestUIHelpers,
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
  protected readonly filterActions = inject(RefTestFilterActions);
  protected readonly queryBuilder = inject(RefTestQueryBuilder);
  protected readonly dataService = inject(RefTestData);
  protected readonly selectionManager = inject(RefTestSelectionManager);
  protected readonly operationManager = inject(RefTestOperationManager);
  protected readonly localStateManager = inject(RefTestLocalStateManager);
  protected readonly columnVisibility = inject(ColumnVisibilityManager);
  protected readonly uiHelpers = inject(RefTestUIHelpers);

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
  protected readonly loadingMore = signal(false);

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
    return this.allLoadedRefTests();
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

  protected readonly allLoadedRefTests = this.localStateManager.createApplyLocalStateComputed(
    () => {
      const queryData = this._queryData();
      if (!queryData) return [];

      const edges = queryData.edges ?? [];
      return edges
        .filter(
          (edge): edge is NonNullable<typeof edge> & { node: RefTestNode } => !!edge && !!edge.node,
        )
        .map((edge) => edge.node);
    },
  );

  // ========================================================================
  // LIFECYCLE
  // ========================================================================

  constructor() {
    // Set callback for filter changes to reset pagination
    this.filterActions.setOnFilterChangeCallback(() => this.handleFilterChange());

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
      this.filterActions.setSearchTerm(searchTerm);
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

          const alreadyLoadedIds = new Set([
            ...baseIds,
            ...this.localStateManager.getAdditionalLoadedRefTests().map((t) => t.id),
          ]);
          const uniqueNew = newRefTests.filter((t) => !alreadyLoadedIds.has(t.id));

          if (uniqueNew.length > 0) {
            this.localStateManager.addLoadedRefTests(uniqueNew);
          }
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
    this.localStateManager.resetAllState();
  }

  // ========================================================================
  // FILTER METHODS (delegated to filter actions)
  // ========================================================================

  protected setStatusFilter(status?: RefTestStatus): void {
    this.filterActions.setStatusFilter(status);
  }

  protected setTitleFilter(titleId?: string): void {
    this.filterActions.setTitleFilter(titleId);
  }

  protected setInvitationFilter(invitationSent?: boolean): void {
    this.filterActions.setInvitationFilter(invitationSent);
  }

  protected setResultsFilter(resultsSent?: boolean): void {
    this.filterActions.setResultsFilter(resultsSent);
  }

  protected setSorting(sortField: SortField, sortDirection: SortEnumType): void {
    this.filterActions.setSorting(sortField, sortDirection);
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
    this.filterActions.setPerformanceFilters(performance);
  }

  protected setDateRange(type: 'started' | 'completed', after?: string, before?: string): void {
    this.filterActions.setDateRange(type, after, before);
  }

  protected sortByColumn(field: SortField): void {
    this.filterActions.sortByColumn(field);
  }

  protected getAriaSort(field: SortField): 'none' | 'ascending' | 'descending' {
    return this.filterActions.getAriaSort(field);
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
  // UI HELPERS (delegated to UI helpers service)
  // ========================================================================

  protected getStatusClass(status: RefTestStatus): string {
    return this.uiHelpers.getStatusClass(status);
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
      this.localStateManager.markAsDeleted(deletedIds);
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
      this.localStateManager.markInvitationsSent(sentIds);
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
      this.localStateManager.markResultsSent(sentIds);
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
