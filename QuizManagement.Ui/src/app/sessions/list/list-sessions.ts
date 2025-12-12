import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  HostListener,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { map } from 'rxjs';
import {
  GetQuizSessionsGQL,
  GetQuizSessionsQuery,
  QuizSessionStatus,
  SortEnumType,
} from '../../../../graphql/generated';
import { SessionFiltersCard } from './components/session-filters-card/session-filters-card';

type SortField =
  | 'completedAt'
  | 'startedAt'
  | 'email'
  | 'score'
  | 'percentage'
  | 'status'
  | 'numberOfQuestions'
  | 'invitationSent';

interface SessionFilter {
  status?: QuizSessionStatus;
  invitationSent?: boolean;
  searchTerm: string;
  sortField: SortField;
  sortDirection: SortEnumType;
}

type SessionNode = NonNullable<
  NonNullable<NonNullable<GetQuizSessionsQuery['quizSessions']>['edges']>[number]
>['node'];

@Component({
  selector: 'app-list-sessions',
  imports: [TranslatePipe, DatePipe, SessionFiltersCard],
  templateUrl: './list-sessions.html',
  styleUrl: './list-sessions.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ListSessions {
  private readonly _getQuizSessionsGQL = inject(GetQuizSessionsGQL);
  private readonly _router = inject(Router);

  protected readonly filter = signal<SessionFilter>({
    searchTerm: '',
    sortField: 'completedAt',
    sortDirection: SortEnumType.Desc,
  });

  protected readonly QuizSessionStatus = QuizSessionStatus;
  protected readonly SortEnumType = SortEnumType;

  private readonly pageSize = 20;
  protected readonly loadingMore = signal(false);
  protected readonly allLoadedSessions = signal<SessionNode[]>([]);
  private endCursor = signal<string | undefined>(undefined);
  protected readonly hasNextPage = signal(false);

  private readonly queryRef = this._getQuizSessionsGQL.watch({
    variables: {
      first: this.pageSize,
      where: this.buildWhereFilter(),
      order: this.buildOrderClause(),
    },
    fetchPolicy: 'cache-and-network',
  });

  private readonly queryResult = toSignal(
    this.queryRef.valueChanges.pipe(
      map((result) => {
        if (result.data?.quizSessions) {
          const edges = result.data.quizSessions.edges ?? [];
          const newSessions = edges
            .filter(
              (edge): edge is NonNullable<typeof edge> & { node: SessionNode } =>
                !!edge && !!edge.node
            )
            .map((edge) => edge.node);

          this.allLoadedSessions.set(newSessions);
          this.hasNextPage.set(result.data.quizSessions.pageInfo?.hasNextPage ?? false);
          this.endCursor.set(result.data.quizSessions.pageInfo?.endCursor ?? undefined);
        }
        return result.data?.quizSessions;
      })
    )
  );

  protected readonly loading = toSignal(
    this.queryRef.valueChanges.pipe(map((result) => result.loading)),
    { initialValue: true }
  );

  constructor() {
    effect(() => {
      this.allLoadedSessions.set([]);
      this.queryRef.refetch({
        first: this.pageSize,
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

    this.queryRef
      .fetchMore({
        variables: {
          first: this.pageSize,
          after: this.endCursor(),
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
          this.endCursor.set(result.data.quizSessions.pageInfo?.endCursor ?? undefined);
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

  protected readonly totalCount = computed(() => this.queryResult()?.totalCount ?? 0);

  protected readonly statusCounts = computed(() => {
    const allSessions = this.allSessions();
    return {
      all: allSessions.length,
      pending: allSessions.filter((s) => s.status === QuizSessionStatus.Pending).length,
      inProgress: allSessions.filter((s) => s.status === QuizSessionStatus.InProgress).length,
      completed: allSessions.filter((s) => s.status === QuizSessionStatus.Completed).length,
      expired: allSessions.filter((s) => s.status === QuizSessionStatus.Expired).length,
    };
  });

  private buildWhereFilter() {
    const currentFilter = this.filter();
    const filters: any = {};

    if (currentFilter.status) {
      filters.status = { eq: currentFilter.status };
    }

    if (currentFilter.invitationSent !== undefined) {
      filters.invitationSent = { eq: currentFilter.invitationSent };
    }

    return Object.keys(filters).length > 0 ? filters : undefined;
  }

  private buildOrderClause() {
    const currentFilter = this.filter();
    return [{ [currentFilter.sortField]: currentFilter.sortDirection }];
  }

  protected setStatusFilter(status?: QuizSessionStatus): void {
    this.filter.update((f) => ({ ...f, status }));
  }

  protected setInvitationFilter(invitationSent?: boolean): void {
    this.filter.update((f) => ({ ...f, invitationSent }));
  }

  protected setSorting(sortField: SortField, sortDirection: SortEnumType): void {
    this.filter.update((f) => ({ ...f, sortField, sortDirection }));
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
    this.filter.update((f) => ({ ...f, searchTerm: value }));
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
}
