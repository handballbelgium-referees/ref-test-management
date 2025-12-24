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
import { catchError, debounceTime, finalize, map, of, Subject, tap } from 'rxjs';
import {
  BooleanOperationFilterInput,
  DateTimeOperationFilterInput,
  DeleteQuizSessionsGQL,
  FloatOperationFilterInput,
  GetQuizSessionsCountGQL,
  GetQuizSessionsGQL,
  GetQuizSessionsQuery,
  IntOperationFilterInput,
  QuizSessionFilterInput,
  QuizSessionStatus,
  QuizSessionStatusOperationFilterInput,
  QuizTitleFilterInput,
  SendQuizInvitationsGQL,
  SendQuizResultsGQL,
  SortEnumType,
} from '../../../../graphql/generated';
import { ColumnVisibilityMenu } from './components/column-visibility-menu/column-visibility-menu';
import { DeleteSessionsDialog } from './components/dialogs/delete-sessions-dialog/delete-sessions-dialog';
import { SendInvitationsDialog } from './components/dialogs/send-invitations-dialog/send-invitations-dialog';
import { SendResultsDialog } from './components/dialogs/send-results-dialog/send-results-dialog';
import { SessionFiltersCard } from './components/filters/session-filters-card/session-filters-card';
import { SessionBulkActions } from './components/session-bulk-actions/session-bulk-actions';
import { SessionMobileCard } from './components/session-display/session-mobile-card/session-mobile-card';
import { SessionTableRow } from './components/session-display/session-table-row/session-table-row';

type SortField =
  | 'title'
  | 'completedAt'
  | 'startedAt'
  | 'email'
  | 'score'
  | 'percentage'
  | 'status'
  | 'numberOfQuestions'
  | 'invitationSent'
  | 'resultsSent';

interface ISessionFilter {
  status?: QuizSessionStatus;
  invitationSent?: boolean;
  resultsSent?: boolean;
  titleValue?: string;
  searchTerm: string;
  sortField: SortField;
  sortDirection: SortEnumType;
  minScore?: number;
  maxScore?: number;
  percentageRange?: 'low' | 'medium' | 'high';
  minQuestions?: number;
  maxQuestions?: number;
  startedAfter?: string;
  startedBefore?: string;
  completedAfter?: string;
  completedBefore?: string;
}

type SessionNode = NonNullable<
  NonNullable<NonNullable<GetQuizSessionsQuery['quizSessions']>['edges']>[number]
>['node'];

@Component({
  selector: 'app-list-sessions',
  imports: [
    TranslatePipe,
    SessionFiltersCard,
    SessionBulkActions,
    SessionTableRow,
    SessionMobileCard,
    ColumnVisibilityMenu,
    SendInvitationsDialog,
    SendResultsDialog,
    DeleteSessionsDialog,
  ],
  templateUrl: './list-sessions.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class ListSessions {
  private readonly _getQuizSessionsGQL = inject(GetQuizSessionsGQL);
  private readonly _getQuizSessionsCountGQL = inject(GetQuizSessionsCountGQL);
  private readonly _deleteQuizSessionsGQL = inject(DeleteQuizSessionsGQL);
  private readonly _sendInvitationsGQL = inject(SendQuizInvitationsGQL);
  private readonly _sendResultsGQL = inject(SendQuizResultsGQL);
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

  protected readonly filter = signal<ISessionFilter>({
    searchTerm: '',
    sortField: 'completedAt',
    sortDirection: SortEnumType.Desc,
  });

  protected readonly QuizSessionStatus = QuizSessionStatus;
  protected readonly SortEnumType = SortEnumType;

  private readonly _pageSize = 20;
  protected readonly loadingMore = signal(false);
  protected readonly allLoadedSessions = signal<SessionNode[]>([]);
  private _endCursor = signal<string | undefined>(undefined);
  protected readonly hasNextPage = signal(false);
  protected readonly deletingSessionIds = signal<Set<string>>(new Set());
  protected readonly sendingInvitationIds = signal<Set<string>>(new Set());
  protected readonly sendingResultsIds = signal<Set<string>>(new Set());
  protected readonly selectedSessionIds = signal<Set<string>>(new Set());
  protected readonly showSendInvitationsDialog = signal(false);
  protected readonly showSendResultsDialog = signal(false);
  protected readonly showDeleteDialog = signal(false);
  protected readonly sendingInvitations = signal(false);
  protected readonly sendingResults = signal(false);
  protected readonly deletingSessions = signal(false);

  protected readonly invitationSummary = computed(() => {
    const selectedIds = this.selectedSessionIds();
    const allSessions = this.allLoadedSessions();
    const selected = allSessions.filter(
      (s) => selectedIds.has(s.id) && s.status === QuizSessionStatus.Pending
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
    const selectedIds = this.selectedSessionIds();
    const allSessions = this.allLoadedSessions();
    const selected = allSessions.filter(
      (s) => selectedIds.has(s.id) && s.status === QuizSessionStatus.Completed
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

  protected readonly sessionsToDelete = computed(() => {
    const selectedIds = this.selectedSessionIds();
    const allSessions = this.allLoadedSessions();
    return allSessions
      .filter((s) => selectedIds.has(s.id))
      .map((s) => ({ name: s.name || '', email: s.email || '' }));
  });

  protected readonly hasCompletedSessionsSelected = computed(() => {
    const selectedIds = this.selectedSessionIds();
    const allSessions = this.allLoadedSessions();
    return allSessions.some(
      (s) => selectedIds.has(s.id) && s.status === QuizSessionStatus.Completed
    );
  });

  protected readonly hasPendingSessionsSelected = computed(() => {
    const selectedIds = this.selectedSessionIds();
    const allSessions = this.allLoadedSessions();
    return allSessions.some((s) => selectedIds.has(s.id) && s.status === QuizSessionStatus.Pending);
  });

  protected readonly allSelected = computed(() => {
    const sessions = this.sessions();
    const selected = this.selectedSessionIds();
    return sessions.length > 0 && sessions.every((s) => selected.has(s.id));
  });

  protected readonly someSelected = computed(() => {
    const sessions = this.sessions();
    const selected = this.selectedSessionIds();
    return sessions.some((s) => selected.has(s.id)) && !this.allSelected();
  });

  protected readonly selectedCount = computed(() => this.selectedSessionIds().size);

  private readonly _queryRef = this._getQuizSessionsGQL.watch({
    variables: {
      first: this._pageSize,
      where: this.buildWhereFilter(),
      order: this.buildOrderClause(),
    },
    fetchPolicy: 'cache-and-network',
  });

  private readonly _allCountRef = this._getQuizSessionsCountGQL.watch({
    fetchPolicy: 'cache-and-network',
  });
  private readonly _pendingCountRef = this._getQuizSessionsCountGQL.watch({
    variables: { where: { status: { eq: QuizSessionStatus.Pending } } },
    fetchPolicy: 'cache-and-network',
  });
  private readonly _inProgressCountRef = this._getQuizSessionsCountGQL.watch({
    variables: { where: { status: { eq: QuizSessionStatus.InProgress } } },
    fetchPolicy: 'cache-and-network',
  });
  private readonly _completedCountRef = this._getQuizSessionsCountGQL.watch({
    variables: { where: { status: { eq: QuizSessionStatus.Completed } } },
    fetchPolicy: 'cache-and-network',
  });
  private readonly _expiredCountRef = this._getQuizSessionsCountGQL.watch({
    variables: { where: { status: { eq: QuizSessionStatus.Expired } } },
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

    // React to query results
    effect(() => {
      const result = this._queryResult();
      if (result?.data?.quizSessions) {
        const edges = result.data.quizSessions.edges ?? [];
        const newSessions = edges
          .filter(
            (edge): edge is NonNullable<typeof edge> & { node: SessionNode } =>
              !!edge && !!edge.node
          )
          .map((edge) => edge.node);

        this.allLoadedSessions.set(newSessions);
        this.hasNextPage.set(result.data.quizSessions.pageInfo?.hasNextPage ?? false);
        this._endCursor.set(result.data.quizSessions.pageInfo?.endCursor ?? undefined);
      }
    });

    // React to filter changes
    effect(() => {
      this.filter(); // Track the signal
      this.allLoadedSessions.set([]);
      this._queryRef.refetch({
        first: this._pageSize,
        after: undefined,
        where: this.buildWhereFilter(),
        order: this.buildOrderClause(),
      });
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
        if (result.data?.quizSessions) {
          const edges = result.data.quizSessions.edges ?? [];
          const newSessions = edges
            .filter(
              (edge): edge is NonNullable<typeof edge> & { node: SessionNode } =>
                !!edge && !!edge.node
            )
            .map((edge) => edge.node);

          this.allLoadedSessions.update((current) => [...current, ...newSessions]);
          this.hasNextPage.set(result.data.quizSessions.pageInfo?.hasNextPage ?? false);
          this._endCursor.set(result.data.quizSessions.pageInfo?.endCursor ?? undefined);
        }
        this.loadingMore.set(false);
      })
      .catch(() => {
        this.loadingMore.set(false);
      });
  }

  protected readonly allSessions = computed((): SessionNode[] => {
    return this.allLoadedSessions();
  });

  protected readonly sessions = computed((): SessionNode[] => {
    const sessions = this.allSessions();
    const searchTerm = this.filter().searchTerm.toLowerCase();

    if (!searchTerm) {
      return sessions;
    }

    return sessions.filter(
      (session) =>
        session.name?.toLowerCase().includes(searchTerm) ||
        session.email?.toLowerCase().includes(searchTerm)
    );
  });

  protected readonly totalCount = computed(
    () => this._queryRef.getCurrentResult()?.data?.quizSessions?.totalCount ?? 0
  );

  private readonly _allCountResult = toSignal(this._allCountRef.valueChanges);
  private readonly _pendingCountResult = toSignal(this._pendingCountRef.valueChanges);
  private readonly _inProgressCountResult = toSignal(this._inProgressCountRef.valueChanges);
  private readonly _completedCountResult = toSignal(this._completedCountRef.valueChanges);
  private readonly _expiredCountResult = toSignal(this._expiredCountRef.valueChanges);

  protected readonly statusCounts = computed(() => ({
    all: this._allCountResult()?.data?.quizSessions?.totalCount ?? 0,
    pending: this._pendingCountResult()?.data?.quizSessions?.totalCount ?? 0,
    inProgress: this._inProgressCountResult()?.data?.quizSessions?.totalCount ?? 0,
    completed: this._completedCountResult()?.data?.quizSessions?.totalCount ?? 0,
    expired: this._expiredCountResult()?.data?.quizSessions?.totalCount ?? 0,
  }));

  private buildWhereFilter(): QuizSessionFilterInput | undefined {
    const currentFilter = this.filter();
    const filters: QuizSessionFilterInput = {};

    if (currentFilter.status) {
      filters.status = { eq: currentFilter.status } as QuizSessionStatusOperationFilterInput;
    }

    if (currentFilter.titleValue) {
      filters.title = { value: { eq: currentFilter.titleValue } } as QuizTitleFilterInput;
    }

    if (currentFilter.invitationSent !== undefined) {
      filters.invitationSent = { eq: currentFilter.invitationSent } as BooleanOperationFilterInput;
    }

    if (currentFilter.resultsSent !== undefined) {
      filters.resultsSent = { eq: currentFilter.resultsSent } as BooleanOperationFilterInput;
    }

    if (currentFilter.minScore !== undefined || currentFilter.maxScore !== undefined) {
      const scoreFilter: IntOperationFilterInput = {};
      if (currentFilter.minScore !== undefined) {
        scoreFilter.gte = currentFilter.minScore;
      }
      if (currentFilter.maxScore !== undefined) {
        scoreFilter.lte = currentFilter.maxScore;
      }
      filters.score = scoreFilter;
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

  protected setStatusFilter(status?: QuizSessionStatus): void {
    this.filter.update((f) => ({ ...f, status }));
  }

  protected setTitleFilter(titleId?: string): void {
    this.filter.update((f) => ({ ...f, titleValue: titleId }));
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

  protected navigateToCreate(): void {
    this._router.navigate(['/sessions/create']);
  }

  protected getStatusClass(status: QuizSessionStatus): string {
    switch (status) {
      case QuizSessionStatus.Pending:
        return 'bg-yellow-100 text-yellow-800';
      case QuizSessionStatus.InProgress:
        return 'bg-blue-100 text-blue-800';
      case QuizSessionStatus.Completed:
        return 'bg-green-100 text-green-800';
      case QuizSessionStatus.Expired:
        return 'bg-red-100 text-red-800';
      default:
        return 'bg-neutral-100 text-neutral-800';
    }
  }

  protected deleteSelectedSessions(): void {
    const sessionIds = Array.from(this.selectedSessionIds());
    if (sessionIds.length === 0) {
      return;
    }

    this.showDeleteDialog.set(true);
  }

  protected confirmDelete(): void {
    this.showDeleteDialog.set(false);
    const sessionIds = Array.from(this.selectedSessionIds());

    sessionIds.forEach((id) => {
      this.deletingSessionIds.update((ids) => new Set(ids).add(id));
    });

    this._deleteQuizSessionsGQL
      .mutate({
        variables: { input: { ids: sessionIds } },
        fetchPolicy: 'no-cache',
      })
      .pipe(
        tap((result) => this.deletingSessions.set(result.loading ?? false)),
        tap((result) => {
          const deleteResult = result.data?.deleteQuizSessions?.deleteQuizSessionsResult;
          if (deleteResult && deleteResult.deletedSessions.length > 0) {
            const deletedIds = new Set(deleteResult.deletedSessions.map((s) => s.id));
            this.allLoadedSessions.update((sessions) =>
              sessions.filter((s) => !deletedIds.has(s.id))
            );
            this.selectedSessionIds.set(new Set());

            // Refetch counts
            this._allCountRef.refetch();
            this._pendingCountRef.refetch();
            this._inProgressCountRef.refetch();
            this._completedCountRef.refetch();
            this._expiredCountRef.refetch();
          }
        }),
        catchError(() => of(null)),
        finalize(() => {
          sessionIds.forEach((id) => {
            this.deletingSessionIds.update((ids) => {
              const newIds = new Set(ids);
              newIds.delete(id);
              return newIds;
            });
          });
          this.deletingSessions.set(false);
        })
      )
      .subscribe();
  }

  protected cancelDelete(): void {
    this.showDeleteDialog.set(false);
  }

  protected sendResultsToSelected(): void {
    const selectedIds = Array.from(this.selectedSessionIds());
    if (selectedIds.length === 0) {
      return;
    }

    this.showSendResultsDialog.set(true);
  }

  protected confirmSendResults(): void {
    this.showSendResultsDialog.set(false);

    const selectedIds = Array.from(this.selectedSessionIds());
    const allSessions = this.allLoadedSessions();

    // Only send results to completed sessions
    const sessionIds = selectedIds.filter((id) => {
      const session = allSessions.find((s) => s.id === id);
      return session?.status === QuizSessionStatus.Completed;
    });

    sessionIds.forEach((id) => {
      this.sendingResultsIds.update((ids) => new Set(ids).add(id));
    });

    this._sendResultsGQL
      .mutate({ variables: { input: { ids: sessionIds } } })
      .pipe(
        tap((result) => this.sendingResults.set(result.loading ?? false)),
        tap((result) => {
          const sendResult = result.data?.sendResults?.sendResultsResult;
          if (sendResult) {
            const sentIds = new Set(sendResult.sentSessions.map((s) => s.id));
            this.allLoadedSessions.update((sessions) =>
              sessions.map((s) => (sentIds.has(s.id) ? { ...s, resultsSent: true } : s))
            );
            this.selectedSessionIds.set(new Set());
          }
        }),
        catchError(() => {
          return of(null);
        }),
        finalize(() => {
          sessionIds.forEach((id) => {
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
    const sessionIds = Array.from(this.selectedSessionIds());
    if (sessionIds.length === 0) {
      return;
    }

    this.showSendInvitationsDialog.set(true);
  }

  protected confirmSendInvitations(): void {
    this.showSendInvitationsDialog.set(false);
    const selectedIds = Array.from(this.selectedSessionIds());
    const allSessions = this.allLoadedSessions();

    // Only send invitations to pending sessions
    const sessionIds = selectedIds.filter((id) => {
      const session = allSessions.find((s) => s.id === id);
      return session?.status === QuizSessionStatus.Pending;
    });

    sessionIds.forEach((id) => {
      this.sendingInvitationIds.update((ids) => new Set(ids).add(id));
    });

    this._sendInvitationsGQL
      .mutate({
        variables: { input: { ids: sessionIds } },
        fetchPolicy: 'no-cache',
      })
      .pipe(
        tap((result) => this.sendingInvitations.set(result.loading ?? false)),
        tap((result) => {
          const sendResult = result.data?.sendInvitations?.sendInvitationsResult;
          if (sendResult && sendResult.sentSessions.length > 0) {
            const sentIds = new Set(sendResult.sentSessions.map((s) => s.id));
            this.allLoadedSessions.update((sessions) =>
              sessions.map((s) => (sentIds.has(s.id) ? { ...s, invitationSent: true } : s))
            );
            this.selectedSessionIds.set(new Set());
          }
        }),
        catchError(() => of(null)),
        finalize(() => {
          sessionIds.forEach((id) => {
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

  protected isDeleting(sessionId: string): boolean {
    return this.deletingSessionIds().has(sessionId);
  }

  protected isSendingInvitation(sessionId: string): boolean {
    return this.sendingInvitationIds().has(sessionId);
  }

  protected toggleSelectAll(): void {
    if (this.allSelected()) {
      this.selectedSessionIds.set(new Set());
    } else {
      const allIds = this.sessions().map((s) => s.id);
      this.selectedSessionIds.set(new Set(allIds));
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

  protected toggleSessionSelection(sessionId: string): void {
    this.selectedSessionIds.update((ids) => {
      const newIds = new Set(ids);
      if (newIds.has(sessionId)) {
        newIds.delete(sessionId);
      } else {
        newIds.add(sessionId);
      }
      return newIds;
    });
  }

  protected isSelected(sessionId: string): boolean {
    return this.selectedSessionIds().has(sessionId);
  }
}
