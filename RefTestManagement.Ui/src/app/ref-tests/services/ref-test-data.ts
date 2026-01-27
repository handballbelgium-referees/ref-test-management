import { computed, DestroyRef, inject, Injectable, WritableSignal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ApolloCache, ApolloClient, FetchResult } from '@apollo/client';
import { catchError, EMPTY, map, switchMap, tap } from 'rxjs';
import {
  DeleteRefTestsGQL,
  GetRefTestsAllCountsGQL,
  GetRefTestsAllCountsQuery,
  GetRefTestsGQL,
  GetRefTestsQuery,
  RefTestResetType,
  RefTestStatus,
  RefTestsUpdatedGQL,
  ResetRefTestsGQL,
  ReviveRefTestsGQL,
  SendRefTestInvitationsGQL,
  SendRefTestResultsGQL,
  SendReportGQL,
} from '../../../../graphql/generated';
import {
  decrement,
  increment,
  MutationCallbacks,
  runMutation,
} from '../../shared/utils/apollo-utils';
import { REF_TEST_CONFIG } from '../list/services/constants';
import { RefTestFilterState } from '../list/services/ref-test-filter-state';
import { RefTestQueryBuilder } from '../list/services/ref-test-query-builder';
import { IReportResult } from '../list/services/types';

/* -------------------------------------------------------------------------- */
/* Types                                                                      */
/* -------------------------------------------------------------------------- */

type DeletionCounts = {
  total: number;
  pending: number;
  inProgress: number;
  completed: number;
  expired: number;
};

/* -------------------------------------------------------------------------- */
/* Service                                                                    */
/* -------------------------------------------------------------------------- */

@Injectable({ providedIn: 'root' })
export class RefTestData {
  private readonly _getRefTestsGQL = inject(GetRefTestsGQL);
  private readonly _getRefTestsAllCountsGQL = inject(GetRefTestsAllCountsGQL);
  private readonly _deleteRefTestsGQL = inject(DeleteRefTestsGQL);
  private readonly _sendInvitationsGQL = inject(SendRefTestInvitationsGQL);
  private readonly _sendResultsGQL = inject(SendRefTestResultsGQL);
  private readonly _sendReportGQL = inject(SendReportGQL);
  private readonly _resetRefTestsGQL = inject(ResetRefTestsGQL);
  private readonly _reviveRefTestsGQL = inject(ReviveRefTestsGQL);
  private readonly _refTestsUpdatedGQL = inject(RefTestsUpdatedGQL);
  private readonly _filterState = inject(RefTestFilterState);
  private readonly _queryBuilder = inject(RefTestQueryBuilder);

  /* ------------------------------------------------------------------------ */
  /* Queries                                                                  */
  /* ------------------------------------------------------------------------ */

  private readonly _queryRef = this._getRefTestsGQL.watch({
    variables: {
      first: this._filterState.filter().pagingInfo.first,
      after: this._filterState.filter().pagingInfo.after,
      where: this._queryBuilder.buildWhereFilter(this._filterState.filter()),
      order: this._queryBuilder.buildOrderClause(this._filterState.filter()),
    },
  });

  private readonly _countsQueryRef = this._getRefTestsAllCountsGQL.watch({
    variables: this.buildCountsVariables(),
  });

  readonly queryResult = toSignal(
    toObservable(this._filterState.filter).pipe(
      switchMap((filter) => {
        this._queryRef.setVariables({
          first: filter.pagingInfo.first,
          after: filter.pagingInfo.after,
          where: this._queryBuilder.buildWhereFilter(filter),
          order: this._queryBuilder.buildOrderClause(filter),
        });
        return this._queryRef.valueChanges;
      }),
    ),
  );

  readonly loading = computed(() => this.queryResult()?.loading ?? false);

  readonly hasData = computed(() => (this.queryResult()?.data?.refTests?.edges?.length ?? 0) > 0);

  readonly hasNextPage = computed(
    () => this.queryResult()?.data?.refTests?.pageInfo?.hasNextPage ?? false,
  );

  readonly showPerformanceWarning = computed(() => {
    const loaded = this.queryResult()?.data?.refTests?.edges?.length ?? 0;
    return (
      loaded >= REF_TEST_CONFIG.PERFORMANCE_WARNING_THRESHOLD &&
      loaded < REF_TEST_CONFIG.MAX_LOADABLE_ITEMS &&
      this.hasNextPage()
    );
  });

  readonly canLoadMore = computed(() => {
    const loaded = this.queryResult()?.data?.refTests?.edges?.length ?? 0;
    return this.hasNextPage() && loaded < REF_TEST_CONFIG.MAX_LOADABLE_ITEMS;
  });

  readonly isAtMaxCapacity = computed(() => {
    const loaded = this.queryResult()?.data?.refTests?.edges?.length ?? 0;
    return loaded >= REF_TEST_CONFIG.MAX_LOADABLE_ITEMS && this.hasNextPage();
  });

  readonly statusCounts = toSignal(
    toObservable(this._filterState.filter).pipe(
      switchMap(() => {
        this._countsQueryRef.setVariables(this.buildCountsVariables());
        return this._countsQueryRef.valueChanges;
      }),
      map((r) => ({
        all: r.data?.all?.totalCount ?? 0,
        pending: r.data?.pending?.totalCount ?? 0,
        inProgress: r.data?.inProgress?.totalCount ?? 0,
        completed: r.data?.completed?.totalCount ?? 0,
        expired: r.data?.expired?.totalCount ?? 0,
      })),
    ),
    {
      initialValue: { all: 0, pending: 0, inProgress: 0, completed: 0, expired: 0 },
    },
  );

  /* ------------------------------------------------------------------------ */
  /* Pagination / Reset                                                       */
  /* ------------------------------------------------------------------------ */

  fetchMore(): Promise<ApolloClient.QueryResult<GetRefTestsQuery>> {
    return this._queryRef.fetchMore({
      variables: {
        first: this._filterState.filter().pagingInfo.first,
        after: this.queryResult()?.data?.refTests?.pageInfo?.endCursor,
        where: this._queryBuilder.buildWhereFilter(this._filterState.filter()),
        order: this._queryBuilder.buildOrderClause(this._filterState.filter()),
      },
    });
  }

  reset(): void {
    this._queryRef.refetch();
    this._countsQueryRef.refetch();
  }

  /* ------------------------------------------------------------------------ */
  /* Mutations (Public API)                                                   */
  /* ------------------------------------------------------------------------ */

  deleteRefTests(
    ids: string[],
    loadingSignal: WritableSignal<boolean>,
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<string[]> = {},
  ): void {
    runMutation(
      this._deleteRefTestsGQL.mutate({
        variables: { input: { ids } },
        update: this.updateDeleteCache.bind(this),
      }),
      callbacks,
      destroyRef,
      loadingSignal,
      (r) => r.data?.deleteRefTests?.deleteRefTestsResult?.deletedRefTests.map((d) => d.id) ?? [],
    );
  }

  sendInvitations(
    ids: string[],
    loadingSignal: WritableSignal<boolean>,
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<string[]> = {},
  ): void {
    runMutation(
      this._sendInvitationsGQL.mutate({ variables: { input: { ids } } }),
      callbacks,
      destroyRef,
      loadingSignal,
      (r) => r.data?.sendInvitations?.sendInvitationsResult?.sentRefTests.map((s) => s.id) ?? [],
    );
  }

  sendResults(
    ids: string[],
    loadingSignal: WritableSignal<boolean>,
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<string[]> = {},
  ): void {
    runMutation(
      this._sendResultsGQL.mutate({ variables: { input: { ids } } }),
      callbacks,
      destroyRef,
      loadingSignal,
      (r) => r.data?.sendResults?.sendResultsResult?.sentRefTests.map((s) => s.id) ?? [],
    );
  }

  generateReport(
    ids: string[],
    loadingSignal: WritableSignal<boolean>,
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<IReportResult> = {},
  ): void {
    runMutation(
      this._sendReportGQL.mutate({ variables: { input: { ids } } }),
      callbacks,
      destroyRef,
      loadingSignal,
      (r) => {
        const res = r.data?.sendReport?.sendReportResult;
        return {
          success: res?.success ?? false,
          refTestCount: res?.refTestCount ?? 0,
        };
      },
    );
  }

  resetRefTests(
    input: {
      ids: string[];
      resetType: RefTestResetType;
      regenerateToken: boolean;
    },
    loadingSignal: WritableSignal<boolean>,
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<{ successCount: number; failedCount: number }> = {},
  ): void {
    runMutation(
      this._resetRefTestsGQL.mutate({
        variables: { input },
      }),
      callbacks,
      destroyRef,
      loadingSignal,
      (r) => {
        const res = r.data?.resetRefTests?.resetRefTestsResult;
        return {
          successCount: res?.successfullyReset ?? 0,
          failedCount: res?.failed ?? 0,
        };
      },
    );
  }

  reviveRefTests(
    ids: string[],
    loadingSignal: WritableSignal<boolean>,
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<{ successCount: number; failedCount: number }> = {},
  ): void {
    runMutation(
      this._reviveRefTestsGQL.mutate({ variables: { input: { ids } } }),
      callbacks,
      destroyRef,
      loadingSignal,
      (r) => {
        const res = r.data?.reviveRefTests?.reviveRefTestsResult;
        return {
          successCount: res?.successfullyRevived ?? 0,
          failedCount: res?.failed ?? 0,
        };
      },
    );
  }

  /* ------------------------------------------------------------------------ */
  /* Delete Cache Logic                                                       */
  /* ------------------------------------------------------------------------ */

  private updateDeleteCache(cache: ApolloCache, { data }: FetchResult<any>): void {
    const deleted = data?.deleteRefTests?.deleteRefTestsResult?.deletedRefTests ?? [];

    if (!deleted.length) return;

    const counts = this.countDeletionsByStatus(deleted);
    this.updateCountsCache(cache, counts);
    this.updateRefTestsListCache(cache, counts.total);
  }

  private countDeletionsByStatus(deleted: Array<{ status: RefTestStatus }>): DeletionCounts {
    return deleted.reduce(
      (acc, { status }) => {
        acc.total++;
        if (status === RefTestStatus.Pending) acc.pending++;
        else if (status === RefTestStatus.InProgress) acc.inProgress++;
        else if (status === RefTestStatus.Completed) acc.completed++;
        else if (status === RefTestStatus.Expired) acc.expired++;
        return acc;
      },
      { total: 0, pending: 0, inProgress: 0, completed: 0, expired: 0 },
    );
  }

  private updateCountsCache(cache: ApolloCache, counts: DeletionCounts): void {
    const prev = cache.readQuery<GetRefTestsAllCountsQuery>({
      query: this._getRefTestsAllCountsGQL.document,
      variables: this._countsQueryRef.variables,
    });

    if (!prev) return;

    cache.writeQuery({
      query: this._getRefTestsAllCountsGQL.document,
      variables: this._countsQueryRef.variables,
      data: {
        all: decrement(prev.all, counts.total),
        pending: decrement(prev.pending, counts.pending),
        inProgress: decrement(prev.inProgress, counts.inProgress),
        completed: decrement(prev.completed, counts.completed),
        expired: decrement(prev.expired, counts.expired),
      },
    });
  }

  private updateRefTestsListCache(cache: ApolloCache, deletedTotal: number): void {
    const prev = cache.readQuery<GetRefTestsQuery>({
      query: this._getRefTestsGQL.document,
      variables: this._queryRef.variables,
    });

    if (!prev?.refTests) return;

    cache.writeQuery({
      query: this._getRefTestsGQL.document,
      variables: this._queryRef.variables,
      data: {
        ...prev,
        refTests: {
          ...prev.refTests,
          totalCount: prev.refTests.totalCount - deletedTotal,
        },
      },
    });
  }

  /* ------------------------------------------------------------------------ */
  /* Subscription + Cache Updates                                             */
  /* ------------------------------------------------------------------------ */

  public subscribeToRefTestUpdates(destroyRef: DestroyRef): void {
    this._refTestsUpdatedGQL
      .subscribe()
      .pipe(
        map((r) => r.data?.refTestsUpdated),
        tap((event) => {
          if (!event) return;

          const current = this._queryRef.getCurrentResult();
          const isLoaded = current?.data?.refTests?.edges?.some((e) => e?.node?.id === event.id);

          if (isLoaded) {
            this.updateRefTestInCache(event.id, event);
          }
        }),
        catchError(() => EMPTY),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe();
  }

  private updateRefTestInCache(id: string, updates: Record<string, unknown>): void {
    const currentData = this._queryRef.getCurrentResult();
    if (!currentData?.data?.refTests) return;

    let oldStatus: RefTestStatus | undefined;
    if ('status' in updates) {
      const edge = currentData.data.refTests.edges?.find((e) => e?.node?.id === id);
      oldStatus = edge?.node?.status;
    }

    // @ts-expect-error Apollo typing
    this._queryRef.updateQuery((prev) => {
      if (!prev?.refTests?.edges) return prev;

      return {
        ...prev,
        refTests: {
          ...prev.refTests,
          edges: prev.refTests.edges.map((edge) => {
            if (!edge || edge.node?.id !== id) return edge;
            return {
              ...edge,
              node: {
                ...edge.node!,
                ...updates,
              },
            };
          }),
        },
      };
    });

    if ('status' in updates && oldStatus !== updates['status']) {
      this.updateStatusCounts(oldStatus, updates['status'] as RefTestStatus);
    }
  }

  private updateStatusCounts(oldStatus: RefTestStatus | undefined, newStatus: RefTestStatus): void {
    const current = this._countsQueryRef.getCurrentResult();
    if (!current?.data) return;

    // @ts-expect-error Apollo typing
    this._countsQueryRef.updateQuery((prev) => {
      if (!prev) return prev;

      const result = { ...prev };

      // Decrement old status
      switch (oldStatus) {
        case RefTestStatus.Pending:
          result.pending = decrement(result.pending);
          break;
        case RefTestStatus.InProgress:
          result.inProgress = decrement(result.inProgress);
          break;
        case RefTestStatus.Completed:
          result.completed = decrement(result.completed);
          break;
        case RefTestStatus.Expired:
          result.expired = decrement(result.expired);
          break;
      }

      // Increment new status
      switch (newStatus) {
        case RefTestStatus.Pending:
          result.pending = increment(result.pending);
          break;
        case RefTestStatus.InProgress:
          result.inProgress = increment(result.inProgress);
          break;
        case RefTestStatus.Completed:
          result.completed = increment(result.completed);
          break;
        case RefTestStatus.Expired:
          result.expired = increment(result.expired);
          break;
      }

      return result;
    });
  }

  /* ------------------------------------------------------------------------ */
  /* Helpers                                                                  */
  /* ------------------------------------------------------------------------ */

  private buildCountsVariables() {
    const base = this._queryBuilder.buildWhereFilter(this._filterState.filter(), {
      excludeStatus: true,
    });

    return {
      allWhere: base,
      pendingWhere: this._queryBuilder.mergeFilters(base, {
        status: { eq: RefTestStatus.Pending },
      }),
      inProgressWhere: this._queryBuilder.mergeFilters(base, {
        status: { eq: RefTestStatus.InProgress },
      }),
      completedWhere: this._queryBuilder.mergeFilters(base, {
        status: { eq: RefTestStatus.Completed },
      }),
      expiredWhere: this._queryBuilder.mergeFilters(base, {
        status: { eq: RefTestStatus.Expired },
      }),
    };
  }
}
