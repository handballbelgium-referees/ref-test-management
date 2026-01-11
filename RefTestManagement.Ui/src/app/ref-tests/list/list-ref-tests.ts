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
import { catchError, debounceTime, finalize, map, of, Subject, tap } from 'rxjs';
import {
  BooleanOperationFilterInput,
  DateTimeOperationFilterInput,
  DeleteRefTestsGQL,
  FloatOperationFilterInput,
  GenerateReportGQL,
  GetRefTestsCountGQL,
  GetRefTestsGQL,
  GetRefTestsQuery,
  GetScoreConfigurationGQL,
  IntOperationFilterInput,
  RefTestFilterInput,
  RefTestStatus,
  RefTestStatusOperationFilterInput,
  RefTestTitleFilterInput,
  SendRefTestInvitationsGQL,
  SendRefTestResultsGQL,
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

type SortField =
  | 'title'
  | 'completedAt'
  | 'startedAt'
  | 'email'
  | 'questionScore'
  | 'answerScore'
  | 'percentage'
  | 'status'
  | 'numberOfQuestions'
  | 'invitationSent'
  | 'resultsSent';

interface IRefTestFilter {
  status?: RefTestStatus;
  invitationSent?: boolean;
  resultsSent?: boolean;
  titleValue?: string;
  searchTerm: string;
  sortField: SortField;
  sortDirection: SortEnumType;
  minQuestionScore?: number;
  maxQuestionScore?: number;
  minAnswerScore?: number;
  maxAnswerScore?: number;
  percentageRange?: 'low' | 'medium' | 'high';
  minQuestions?: number;
  maxQuestions?: number;
  startedAfter?: string;
  startedBefore?: string;
  completedAfter?: string;
  completedBefore?: string;
}

type RefTestNode = NonNullable<
  NonNullable<NonNullable<GetRefTestsQuery['refTests']>['edges']>[number]
>['node'];

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
  templateUrl: './list-ref-tests.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class ListRefTests {
  private readonly _getRefTestsGQL = inject(GetRefTestsGQL);
  private readonly _getRefTestsCountGQL = inject(GetRefTestsCountGQL);
  private readonly _deleteRefTestsGQL = inject(DeleteRefTestsGQL);
  private readonly _sendInvitationsGQL = inject(SendRefTestInvitationsGQL);
  private readonly _sendResultsGQL = inject(SendRefTestResultsGQL);
  private readonly _generateReportGQL = inject(GenerateReportGQL);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);

  private readonly _searchSubject = new Subject<string>();

  protected readonly visibleColumns = signal(
    new Set<string>([
      'title',
      'participant',
      'status',
      'questions',
      'score',
      'invitation',
      'results',
      'started',
      'completed',
    ])
  );
  protected readonly showColumnMenu = signal(false);

  protected readonly filter = signal<IRefTestFilter>({
    searchTerm: '',
    sortField: 'completedAt',
    sortDirection: SortEnumType.Desc,
  });

  protected readonly RefTestStatus = RefTestStatus;
  protected readonly SortEnumType = SortEnumType;

  private readonly _pageSize = 20;
  protected readonly loadingMore = signal(false);
  protected readonly allLoadedRefTests = signal<RefTestNode[]>([]);
  private _endCursor = signal<string | undefined>(undefined);
  protected readonly hasNextPage = signal(false);
  protected readonly deletingRefTestIds = signal<Set<string>>(new Set());
  protected readonly sendingInvitationIds = signal<Set<string>>(new Set());
  protected readonly sendingResultsIds = signal<Set<string>>(new Set());
  protected readonly selectedRefTestIds = signal<Set<string>>(new Set());
  protected readonly showSendInvitationsDialog = signal(false);
  protected readonly showSendResultsDialog = signal(false);
  protected readonly showDeleteDialog = signal(false);
  protected readonly showGenerateReportDialog = signal(false);
  protected readonly sendingInvitations = signal(false);
  protected readonly sendingResults = signal(false);
  protected readonly deletingRefTests = signal(false);
  protected readonly generatingReport = signal(false);
  protected readonly reportResult = signal<{
    success: boolean;
    refTestCount: number;
  } | null>(null);

  protected readonly invitationSummary = computed(() => {
    const selectedIds = this.selectedRefTestIds();
    const allRefTests = this.allLoadedRefTests();
    const selected = allRefTests.filter(
      (s) => selectedIds.has(s.id) && s.status === RefTestStatus.Pending
    );

    return {
      newInvitations: selected
        .filter((s) => !s.invitationSent)
        .map((s) => ({ name: s.name || '', email: s.email || '' })),
      resendInvitations: selected
        .filter((s) => s.invitationSent)
        .map((s) => ({ name: s.name || '', email: s.email || '' })),
    };
  });

  protected readonly resultsSummary = computed(() => {
    const selectedIds = this.selectedRefTestIds();
    const allRefTests = this.allLoadedRefTests();
    const selected = allRefTests.filter(
      (s) => selectedIds.has(s.id) && s.status === RefTestStatus.Completed
    );

    return {
      newResults: selected
        .filter((s) => !s.resultsSent)
        .map((s) => ({ name: s.name || '', email: s.email || '' })),
      resendResults: selected
        .filter((s) => s.resultsSent)
        .map((s) => ({ name: s.name || '', email: s.email || '' })),
    };
  });

  protected readonly refTestsToDelete = computed(() => {
    const selectedIds = this.selectedRefTestIds();
    const allRefTests = this.allLoadedRefTests();
    return allRefTests
      .filter((s) => selectedIds.has(s.id))
      .map((s) => ({ name: s.name || '', email: s.email || '' }));
  });

  protected readonly reportSummary = computed(() => {
    const selectedIds = this.selectedRefTestIds();
    const allRefTests = this.allLoadedRefTests();
    return {
      refTests: allRefTests
        .filter((s) => selectedIds.has(s.id))
        .map((s) => ({ name: s.name || '', email: s.email || '' })),
    };
  });

  protected readonly hasCompletedRefTestsSelected = computed(() => {
    const selectedIds = this.selectedRefTestIds();
    const allRefTests = this.allLoadedRefTests();
    return allRefTests.some((s) => selectedIds.has(s.id) && s.status === RefTestStatus.Completed);
  });

  protected readonly hasPendingRefTestsSelected = computed(() => {
    const selectedIds = this.selectedRefTestIds();
    const allRefTests = this.allLoadedRefTests();
    return allRefTests.some((s) => selectedIds.has(s.id) && s.status === RefTestStatus.Pending);
  });

  protected readonly allSelected = computed(() => {
    const refTests = this.refTests();
    const selected = this.selectedRefTestIds();
    const totalCount = this.totalCount();

    // If we have selected RefTests equal to total count, all are selected
    if (selected.size > 0 && selected.size === totalCount) {
      return true;
    }

    // Otherwise check if all currently visible RefTests are selected
    return refTests.length > 0 && refTests.every((s) => selected.has(s.id));
  });

  protected readonly someSelected = computed(() => {
    const refTests = this.refTests();
    const selected = this.selectedRefTestIds();
    return refTests.some((s) => selected.has(s.id)) && !this.allSelected();
  });

  protected readonly selectedCount = computed(() => this.selectedRefTestIds().size);

  protected readonly passingPercentage = toSignal(
    inject(GetScoreConfigurationGQL)
      .watch()
      .valueChanges.pipe(
        onlyCompleteData(),
        map((result) => result.data.scoreConfiguration.passingPercentage)
      ),
    { initialValue: 0 }
  );

  private readonly _queryRef = this._getRefTestsGQL.watch({
    variables: {
      first: this._pageSize,
      where: this.buildWhereFilter(),
      order: this.buildOrderClause(),
    },
    fetchPolicy: 'cache-and-network',
  });

  private readonly _allCountRef = this._getRefTestsCountGQL.watch({
    fetchPolicy: 'cache-and-network',
  });
  private readonly _pendingCountRef = this._getRefTestsCountGQL.watch({
    fetchPolicy: 'cache-and-network',
  });
  private readonly _inProgressCountRef = this._getRefTestsCountGQL.watch({
    fetchPolicy: 'cache-and-network',
  });
  private readonly _completedCountRef = this._getRefTestsCountGQL.watch({
    fetchPolicy: 'cache-and-network',
  });
  private readonly _expiredCountRef = this._getRefTestsCountGQL.watch({
    fetchPolicy: 'cache-and-network',
  });

  protected readonly loading = toSignal(
    this._queryRef.valueChanges.pipe(map((result) => result.loading)),
    { initialValue: true }
  );

  private readonly _queryResult = toSignal(this._queryRef.valueChanges);

  constructor() {
    // Debounced search effect
    this._searchSubject
      .pipe(debounceTime(300), takeUntilDestroyed(this._destroyRef))
      .subscribe((searchTerm) => {
        this.filter.update((f) => ({ ...f, searchTerm }));
      });

    // Sync isRefreshing with loading state
    effect(() => {
      const isLoading = this.loading();
      if (!isLoading && this.isRefreshing()) {
        this.isRefreshing.set(false);
      }
    });

    // React to query results
    effect(() => {
      const result = this._queryResult();
      if (result?.data?.refTests) {
        const edges = result.data.refTests.edges ?? [];
        const newRefTests = edges
          .filter(
            (edge): edge is NonNullable<typeof edge> & { node: RefTestNode } =>
              !!edge && !!edge.node
          )
          .map((edge) => edge.node);

        this.allLoadedRefTests.set(newRefTests);
        this.hasNextPage.set(result.data.refTests.pageInfo?.hasNextPage ?? false);
        this._endCursor.set(result.data.refTests.pageInfo?.endCursor ?? undefined);

        // Automatically load all remaining pages
        if (result.data.refTests.pageInfo?.hasNextPage && !this.loadingMore()) {
          this.loadMore();
        }
      }
    });

    // React to filter changes
    effect(() => {
      this.filter(); // Track the signal
      this.allLoadedRefTests.set([]);
      this._queryRef.refetch({
        first: this._pageSize,
        after: undefined,
        where: this.buildWhereFilter(),
        order: this.buildOrderClause(),
      });
      // Update count queries with current filters
      this.updateCountQueries();
    });
  }

  @HostListener('window:scroll')
  onScroll(): void {
    if (this.loadingMore() || !this.hasNextPage()) {
      return;
    }

    const scrollPosition = window.innerHeight + window.scrollY;
    const documentHeight = document.documentElement.scrollHeight;
    const threshold = 200; // Load more when 200px from bottom

    if (scrollPosition >= documentHeight - threshold) {
      this.loadMore();
    }
  }

  protected loadMore(): void {
    if (this.loadingMore() || !this.hasNextPage()) {
      return;
    }

    this.loadingMore.set(true);

    this._queryRef
      .fetchMore({
        variables: {
          first: this._pageSize,
          after: this._endCursor(),
          where: this.buildWhereFilter(),
          order: this.buildOrderClause(),
        },
      })
      .then((result) => {
        if (result.data?.refTests) {
          const edges = result.data.refTests.edges ?? [];
          const newRefTests = edges
            .filter(
              (edge): edge is NonNullable<typeof edge> & { node: RefTestNode } =>
                !!edge && !!edge.node
            )
            .map((edge) => edge.node);

          this.allLoadedRefTests.update((current) => [...current, ...newRefTests]);
          this.hasNextPage.set(result.data.refTests.pageInfo?.hasNextPage ?? false);
          this._endCursor.set(result.data.refTests.pageInfo?.endCursor ?? undefined);
        }
        this.loadingMore.set(false);
      })
      .catch(() => {
        this.loadingMore.set(false);
      });
  }

  protected readonly allRefTests = computed((): RefTestNode[] => {
    return this.allLoadedRefTests();
  });

  protected readonly refTests = computed((): RefTestNode[] => {
    const refTests = this.allRefTests();
    const searchTerm = this.filter().searchTerm.toLowerCase();

    if (!searchTerm) {
      return refTests;
    }

    return refTests.filter(
      (refTest) =>
        refTest.name?.toLowerCase().includes(searchTerm) ||
        refTest.email?.toLowerCase().includes(searchTerm)
    );
  });

  protected readonly totalCount = computed(
    () => this._queryResult()?.data?.refTests?.totalCount ?? 0
  );

  private readonly _allCountResult = toSignal(this._allCountRef.valueChanges);
  private readonly _pendingCountResult = toSignal(this._pendingCountRef.valueChanges);
  private readonly _inProgressCountResult = toSignal(this._inProgressCountRef.valueChanges);
  private readonly _completedCountResult = toSignal(this._completedCountRef.valueChanges);
  private readonly _expiredCountResult = toSignal(this._expiredCountRef.valueChanges);

  protected readonly statusCounts = computed(() => ({
    all: this._allCountResult()?.data?.refTests?.totalCount ?? 0,
    pending: this._pendingCountResult()?.data?.refTests?.totalCount ?? 0,
    inProgress: this._inProgressCountResult()?.data?.refTests?.totalCount ?? 0,
    completed: this._completedCountResult()?.data?.refTests?.totalCount ?? 0,
    expired: this._expiredCountResult()?.data?.refTests?.totalCount ?? 0,
  }));

  private buildWhereFilter(): RefTestFilterInput | undefined {
    const currentFilter = this.filter();
    const filters: RefTestFilterInput = {};

    if (currentFilter.status) {
      filters.status = { eq: currentFilter.status } as RefTestStatusOperationFilterInput;
    }

    if (currentFilter.titleValue) {
      filters.title = { value: { eq: currentFilter.titleValue } } as RefTestTitleFilterInput;
    }

    if (currentFilter.invitationSent !== undefined) {
      filters.invitationSent = { eq: currentFilter.invitationSent } as BooleanOperationFilterInput;
    }

    if (currentFilter.resultsSent !== undefined) {
      filters.resultsSent = { eq: currentFilter.resultsSent } as BooleanOperationFilterInput;
    }

    if (
      currentFilter.minQuestionScore !== undefined ||
      currentFilter.maxQuestionScore !== undefined
    ) {
      const scoreFilter: IntOperationFilterInput = {};
      if (currentFilter.minQuestionScore !== undefined) {
        scoreFilter.gte = currentFilter.minQuestionScore;
      }
      if (currentFilter.maxQuestionScore !== undefined) {
        scoreFilter.lte = currentFilter.maxQuestionScore;
      }
      filters.questionScore = scoreFilter;
    }

    if (currentFilter.minAnswerScore !== undefined || currentFilter.maxAnswerScore !== undefined) {
      const answerScoreFilter: IntOperationFilterInput = {};
      if (currentFilter.minAnswerScore !== undefined) {
        answerScoreFilter.gte = currentFilter.minAnswerScore;
      }
      if (currentFilter.maxAnswerScore !== undefined) {
        answerScoreFilter.lte = currentFilter.maxAnswerScore;
      }
      filters.answerScore = answerScoreFilter;
    }

    if (currentFilter.percentageRange) {
      const percentageFilter: FloatOperationFilterInput = {};
      if (currentFilter.percentageRange === 'low') {
        percentageFilter.lt = 50;
      } else if (currentFilter.percentageRange === 'medium') {
        percentageFilter.gte = 50;
        percentageFilter.lt = 75;
      } else if (currentFilter.percentageRange === 'high') {
        percentageFilter.gte = 75;
      }
      filters.percentage = percentageFilter;
    }

    if (currentFilter.minQuestions !== undefined || currentFilter.maxQuestions !== undefined) {
      const questionsFilter: IntOperationFilterInput = {};
      if (currentFilter.minQuestions !== undefined) {
        questionsFilter.gte = currentFilter.minQuestions;
      }
      if (currentFilter.maxQuestions !== undefined) {
        questionsFilter.lte = currentFilter.maxQuestions;
      }
      filters.numberOfQuestions = questionsFilter;
    }

    if (currentFilter.startedAfter || currentFilter.startedBefore) {
      const startedAtFilter: DateTimeOperationFilterInput = {};
      if (currentFilter.startedAfter) {
        startedAtFilter.gte = currentFilter.startedAfter;
      }
      if (currentFilter.startedBefore) {
        startedAtFilter.lte = currentFilter.startedBefore;
      }
      filters.startedAt = startedAtFilter;
    }

    if (currentFilter.completedAfter || currentFilter.completedBefore) {
      const completedAtFilter: DateTimeOperationFilterInput = {};
      if (currentFilter.completedAfter) {
        completedAtFilter.gte = currentFilter.completedAfter;
      }
      if (currentFilter.completedBefore) {
        completedAtFilter.lte = currentFilter.completedBefore;
      }
      filters.completedAt = completedAtFilter;
    }

    return Object.keys(filters).length > 0 ? filters : undefined;
  }

  private buildWhereFilterExcludingStatus(): RefTestFilterInput | undefined {
    const currentFilter = this.filter();
    const filters: RefTestFilterInput = {};

    // Exclude status filter

    if (currentFilter.titleValue) {
      filters.title = { value: { eq: currentFilter.titleValue } } as RefTestTitleFilterInput;
    }

    if (currentFilter.invitationSent !== undefined) {
      filters.invitationSent = { eq: currentFilter.invitationSent } as BooleanOperationFilterInput;
    }

    if (currentFilter.resultsSent !== undefined) {
      filters.resultsSent = { eq: currentFilter.resultsSent } as BooleanOperationFilterInput;
    }

    if (
      currentFilter.minQuestionScore !== undefined ||
      currentFilter.maxQuestionScore !== undefined
    ) {
      const scoreFilter: IntOperationFilterInput = {};
      if (currentFilter.minQuestionScore !== undefined) {
        scoreFilter.gte = currentFilter.minQuestionScore;
      }
      if (currentFilter.maxQuestionScore !== undefined) {
        scoreFilter.lte = currentFilter.maxQuestionScore;
      }
      filters.questionScore = scoreFilter;
    }

    if (currentFilter.minAnswerScore !== undefined || currentFilter.maxAnswerScore !== undefined) {
      const answerScoreFilter: IntOperationFilterInput = {};
      if (currentFilter.minAnswerScore !== undefined) {
        answerScoreFilter.gte = currentFilter.minAnswerScore;
      }
      if (currentFilter.maxAnswerScore !== undefined) {
        answerScoreFilter.lte = currentFilter.maxAnswerScore;
      }
      filters.answerScore = answerScoreFilter;
    }

    if (currentFilter.percentageRange) {
      const percentageFilter: FloatOperationFilterInput = {};
      if (currentFilter.percentageRange === 'low') {
        percentageFilter.lt = 50;
      } else if (currentFilter.percentageRange === 'medium') {
        percentageFilter.gte = 50;
        percentageFilter.lt = 75;
      } else if (currentFilter.percentageRange === 'high') {
        percentageFilter.gte = 75;
      }
      filters.percentage = percentageFilter;
    }

    if (currentFilter.minQuestions !== undefined || currentFilter.maxQuestions !== undefined) {
      const questionsFilter: IntOperationFilterInput = {};
      if (currentFilter.minQuestions !== undefined) {
        questionsFilter.gte = currentFilter.minQuestions;
      }
      if (currentFilter.maxQuestions !== undefined) {
        questionsFilter.lte = currentFilter.maxQuestions;
      }
      filters.numberOfQuestions = questionsFilter;
    }

    if (currentFilter.startedAfter || currentFilter.startedBefore) {
      const startedAtFilter: DateTimeOperationFilterInput = {};
      if (currentFilter.startedAfter) {
        startedAtFilter.gte = currentFilter.startedAfter;
      }
      if (currentFilter.startedBefore) {
        startedAtFilter.lte = currentFilter.startedBefore;
      }
      filters.startedAt = startedAtFilter;
    }

    if (currentFilter.completedAfter || currentFilter.completedBefore) {
      const completedAtFilter: DateTimeOperationFilterInput = {};
      if (currentFilter.completedAfter) {
        completedAtFilter.gte = currentFilter.completedAfter;
      }
      if (currentFilter.completedBefore) {
        completedAtFilter.lte = currentFilter.completedBefore;
      }
      filters.completedAt = completedAtFilter;
    }

    return Object.keys(filters).length > 0 ? filters : undefined;
  }

  private buildOrderClause() {
    const currentFilter = this.filter();

    // When sorting by participant (email), sort by lastName then firstName
    if (currentFilter.sortField === 'email') {
      return [
        { lastName: currentFilter.sortDirection },
        { firstName: currentFilter.sortDirection },
      ];
    }

    return [{ [currentFilter.sortField]: currentFilter.sortDirection }];
  }

  private updateCountQueries(): void {
    // Build base filter excluding status so we can add specific status filters for each count
    const baseFilter = this.buildWhereFilterExcludingStatus();

    // All count (no additional status filter)
    this._allCountRef.refetch({
      where: baseFilter,
    });

    // Pending count (add pending status to existing filters)
    this._pendingCountRef.refetch({
      where: this.mergeFilters(baseFilter, { status: { eq: RefTestStatus.Pending } }),
    });

    // In-progress count (add in-progress status to existing filters)
    this._inProgressCountRef.refetch({
      where: this.mergeFilters(baseFilter, { status: { eq: RefTestStatus.InProgress } }),
    });

    // Completed count (add completed status to existing filters)
    this._completedCountRef.refetch({
      where: this.mergeFilters(baseFilter, { status: { eq: RefTestStatus.Completed } }),
    });

    // Expired count (add expired status to existing filters)
    this._expiredCountRef.refetch({
      where: this.mergeFilters(baseFilter, { status: { eq: RefTestStatus.Expired } }),
    });
  }

  private mergeFilters(
    baseFilter: RefTestFilterInput | undefined,
    statusFilter: { status: RefTestStatusOperationFilterInput }
  ): RefTestFilterInput {
    // If no base filter, just return status filter
    if (!baseFilter) {
      return statusFilter;
    }

    // If base filter has a status, replace it with the new status filter
    // Otherwise, add the status filter to the base filter
    return {
      ...baseFilter,
      ...statusFilter,
    };
  }

  protected setStatusFilter(status?: RefTestStatus): void {
    this.filter.update((f) => ({ ...f, status }));
  }

  protected setTitleFilter(titleId?: string): void {
    this.filter.update((f) => {
      const newFilter = { ...f };
      if (titleId) {
        newFilter.titleValue = titleId;
      } else {
        delete newFilter.titleValue;
      }
      return newFilter;
    });
  }

  protected setInvitationFilter(invitationSent?: boolean): void {
    this.filter.update((f) => ({ ...f, invitationSent }));
  }

  protected setResultsFilter(resultsSent?: boolean): void {
    this.filter.update((f) => ({ ...f, resultsSent }));
  }

  protected setSorting(sortField: SortField, sortDirection: SortEnumType): void {
    this.filter.update((f) => ({ ...f, sortField, sortDirection }));
  }

  protected setPerformanceFilters(performance: {
    minScore?: number;
    maxScore?: number;
    percentageRange?: 'low' | 'medium' | 'high';
    minQuestions?: number;
    maxQuestions?: number;
  }): void {
    this.filter.update((f) => ({ ...f, ...performance }));
  }

  protected setDateRange(type: 'started' | 'completed', after?: string, before?: string): void {
    if (type === 'started') {
      this.filter.update((f) => ({ ...f, startedAfter: after, startedBefore: before }));
    } else {
      this.filter.update((f) => ({ ...f, completedAfter: after, completedBefore: before }));
    }
  }

  protected sortByColumn(field: SortField): void {
    const currentFilter = this.filter();
    if (currentFilter.sortField === field) {
      // Toggle direction if same field
      const newDirection =
        currentFilter.sortDirection === SortEnumType.Asc ? SortEnumType.Desc : SortEnumType.Asc;
      this.setSorting(field, newDirection);
    } else {
      // Default to descending for new field
      this.setSorting(field, SortEnumType.Desc);
    }
  }

  protected onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this._searchSubject.next(value);
  }

  protected readonly isRefreshing = signal(false);

  protected refresh(): void {
    if (this.isRefreshing()) {
      return;
    }

    this.isRefreshing.set(true);

    this._queryRef.refetch({
      first: this._pageSize,
      after: undefined,
      where: this.buildWhereFilter(),
      order: this.buildOrderClause(),
    });
    this.updateCountQueries();
  }

  protected navigateToCreate(): void {
    this._router.navigate(['/ref-tests/create']);
  }

  protected getStatusClass(status: RefTestStatus): string {
    switch (status) {
      case RefTestStatus.Pending:
        return 'bg-yellow-100 text-yellow-800';
      case RefTestStatus.InProgress:
        return 'bg-blue-100 text-blue-800';
      case RefTestStatus.Completed:
        return 'bg-green-100 text-green-800';
      case RefTestStatus.Expired:
        return 'bg-red-100 text-red-800';
      default:
        return 'bg-neutral-100 text-neutral-800';
    }
  }

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

    refTestIds.forEach((id) => {
      this.deletingRefTestIds.update((ids) => new Set(ids).add(id));
    });

    this._deleteRefTestsGQL
      .mutate({
        variables: { input: { ids: refTestIds } },
        fetchPolicy: 'no-cache',
      })
      .pipe(
        tap((result) => this.deletingRefTests.set(result.loading ?? false)),
        tap((result) => {
          const deleteResult = result.data?.deleteRefTests?.deleteRefTestsResult;
          if (deleteResult && deleteResult.deletedRefTests.length > 0) {
            const deletedIds = new Set(deleteResult.deletedRefTests.map((s) => s.id));
            this.allLoadedRefTests.update((refTests) =>
              refTests.filter((s) => !deletedIds.has(s.id))
            );
            this.selectedRefTestIds.set(new Set());

            // Refetch counts
            this.updateCountQueries();
          }
        }),
        catchError(() => of(null)),
        finalize(() => {
          refTestIds.forEach((id) => {
            this.deletingRefTestIds.update((ids) => {
              const newIds = new Set(ids);
              newIds.delete(id);
              return newIds;
            });
          });
          this.deletingRefTests.set(false);
        })
      )
      .subscribe();
  }

  protected cancelDelete(): void {
    this.showDeleteDialog.set(false);
  }
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
    this.reportResult.set(null); // Clear previous result

    this._generateReportGQL
      .mutate({
        variables: { input: { ids: refTestIds } },
        fetchPolicy: 'no-cache',
      })
      .pipe(
        tap((result) => this.generatingReport.set(result.loading ?? false)),
        tap((result) => {
          const generateResult = result.data?.generateRefTestsReport?.generateReportResult;
          if (generateResult) {
            this.reportResult.set({
              success: generateResult.success,
              refTestCount: generateResult.refTestCount || 0,
            });
            // Auto-hide banner after 10 seconds
            setTimeout(() => {
              this.dismissReportResult();
            }, 10000);
            if (generateResult.success) {
              this.selectedRefTestIds.set(new Set());
            }
          }
        }),
        catchError(() => {
          this.reportResult.set({
            success: false,
            refTestCount: 0,
          });
          // Auto-hide banner after 10 seconds
          setTimeout(() => {
            this.dismissReportResult();
          }, 10000);
          return of(null);
        }),
        finalize(() => {
          this.generatingReport.set(false);
        }),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe();
  }

  protected cancelGenerateReport(): void {
    this.showGenerateReportDialog.set(false);
  }

  protected dismissReportResult(): void {
    this.reportResult.set(null);
  }
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

    // Only send results to completed RefTests
    const refTestIds = selectedIds.filter((id) => {
      const refTest = allRefTests.find((s) => s.id === id);
      return refTest?.status === RefTestStatus.Completed;
    });

    refTestIds.forEach((id) => {
      this.sendingResultsIds.update((ids) => new Set(ids).add(id));
    });

    this._sendResultsGQL
      .mutate({ variables: { input: { ids: refTestIds } } })
      .pipe(
        tap((result) => this.sendingResults.set(result.loading ?? false)),
        tap((result) => {
          const sendResult = result.data?.sendResults?.sendResultsResult;
          if (sendResult) {
            const sentIds = new Set(sendResult.sentRefTests.map((s) => s.id));
            this.allLoadedRefTests.update((refTests) =>
              refTests.map((s) => (sentIds.has(s.id) ? { ...s, resultsSent: true } : s))
            );
            this.selectedRefTestIds.set(new Set());
          }
        }),
        catchError(() => {
          return of(null);
        }),
        finalize(() => {
          refTestIds.forEach((id) => {
            this.sendingResultsIds.update((ids) => {
              const newIds = new Set(ids);
              newIds.delete(id);
              return newIds;
            });
          });
          this.sendingResults.set(false);
        }),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe();
  }

  protected cancelSendResults(): void {
    this.showSendResultsDialog.set(false);
  }

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

    // Only send invitations to pending RefTests
    const refTestIds = selectedIds.filter((id) => {
      const refTest = allRefTests.find((s) => s.id === id);
      return refTest?.status === RefTestStatus.Pending;
    });

    refTestIds.forEach((id) => {
      this.sendingInvitationIds.update((ids) => new Set(ids).add(id));
    });

    this._sendInvitationsGQL
      .mutate({
        variables: { input: { ids: refTestIds } },
        fetchPolicy: 'no-cache',
      })
      .pipe(
        tap((result) => this.sendingInvitations.set(result.loading ?? false)),
        tap((result) => {
          const sendResult = result.data?.sendInvitations?.sendInvitationsResult;
          if (sendResult && sendResult.sentRefTests.length > 0) {
            const sentIds = new Set(sendResult.sentRefTests.map((s) => s.id));
            this.allLoadedRefTests.update((refTests) =>
              refTests.map((s) => (sentIds.has(s.id) ? { ...s, invitationSent: true } : s))
            );
            this.selectedRefTestIds.set(new Set());
          }
        }),
        catchError(() => of(null)),
        finalize(() => {
          refTestIds.forEach((id) => {
            this.sendingInvitationIds.update((ids) => {
              const newIds = new Set(ids);
              newIds.delete(id);
              return newIds;
            });
          });
          this.sendingInvitations.set(false);
        })
      )
      .subscribe();
  }

  protected cancelSendInvitations(): void {
    this.showSendInvitationsDialog.set(false);
  }

  protected isDeleting(refTestId: string): boolean {
    return this.deletingRefTestIds().has(refTestId);
  }

  protected isSendingInvitation(refTestId: string): boolean {
    return this.sendingInvitationIds().has(refTestId);
  }

  protected toggleSelectAll(): void {
    if (this.allSelected()) {
      this.selectedRefTestIds.set(new Set());
    } else {
      const allIds = this.refTests().map((s) => s.id);
      this.selectedRefTestIds.set(new Set(allIds));
    }
  }

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
}
