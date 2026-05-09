import { computed, DestroyRef, inject, Injectable, Signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ApolloCache, ApolloClient, ApolloLink } from '@apollo/client';
import { Apollo } from 'apollo-angular';
import { catchError, EMPTY, map, switchMap, tap } from 'rxjs';
import {
  DeleteRefTestsGQL,
  DeleteRefTestsMutation,
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
  private readonly _apollo = inject(Apollo);
  private readonly _getRefTestsGQL = inject(GetRefTestsGQL);
  /** IDs of deletes initiated by this client that haven't yet been confirmed by the mutation response.
   * Used to suppress the subscription event so the mutation callback is the sole handler. */
  private readonly _pendingDeleteIds = new Set<string>();
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
    // Evict all cached refTests entries (all variable combinations / status tabs)
    // so every tab fetches fresh data on next visit.
    this._apollo.client.cache.evict({ id: 'ROOT_QUERY', fieldName: 'refTests' });
    this._apollo.client.cache.gc();
    this._queryRef.refetch();
    this._countsQueryRef.refetch();
  }

  /* ------------------------------------------------------------------------ */
  /* Mutations (Public API)                                                   */
  /* ------------------------------------------------------------------------ */

  deleteRefTests(
    ids: string[],
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<string[]> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    // Register IDs before the mutation fires so the subscription handler knows to skip
    // them — whichever arrives first (SSE vs mutation response), the mutation callback
    // is the sole handler for self-initiated deletes.
    for (const id of ids) this._pendingDeleteIds.add(id);
    return runMutation(
      this._deleteRefTestsGQL.mutate({
        variables: { input: { ids } },
        update: this.updateDeleteCache.bind(this),
      }),
      destroyRef,
      callbacks,
      (r) => r.data?.deleteRefTests?.deleteRefTestsResult?.deletedRefTests.map((d) => d.id) ?? [],
    );
  }

  sendInvitations(
    ids: string[],
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<string[]> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    return runMutation(
      this._sendInvitationsGQL.mutate({ variables: { input: { ids } } }),
      destroyRef,
      callbacks,
      (r) => r.data?.sendInvitations?.sendInvitationsResult?.sentRefTests.map((s) => s.id) ?? [],
    );
  }

  sendResults(
    ids: string[],
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<string[]> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    return runMutation(
      this._sendResultsGQL.mutate({ variables: { input: { ids } } }),
      destroyRef,
      callbacks,
      (r) => r.data?.sendResults?.sendResultsResult?.sentRefTests.map((s) => s.id) ?? [],
    );
  }

  generateReport(
    ids: string[],
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<IReportResult> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    return runMutation(
      this._sendReportGQL.mutate({ variables: { input: { ids } } }),
      destroyRef,
      callbacks,
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
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<{ successCount: number; failedCount: number }> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    return runMutation(
      this._resetRefTestsGQL.mutate({
        variables: { input },
      }),
      destroyRef,
      callbacks,
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
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<{ successCount: number; failedCount: number }> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    return runMutation(
      this._reviveRefTestsGQL.mutate({ variables: { input: { ids } } }),
      destroyRef,
      callbacks,
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

  private updateDeleteCache(
    cache: ApolloCache,
    { data }: ApolloLink.Result<DeleteRefTestsMutation>,
  ): void {
    const deleted = data?.deleteRefTests?.deleteRefTestsResult?.deletedRefTests ?? [];

    if (!deleted.length) return;

    // Clear pending tracking so the subscription handler knows these are done.
    for (const d of deleted) this._pendingDeleteIds.delete(d.id);

    const counts = this.countDeletionsByStatus(deleted);
    this.updateCountsCache(cache, counts);
    this.removeFromAllCachedStatusLists(deleted);
  }

  private countDeletionsByStatus(deleted: Array<{ status: RefTestStatus }>): DeletionCounts {
    return deleted.reduce(
      (acc, { status }) => {
        acc.total++;
        if (status === 'PENDING') acc.pending++;
        else if (status === 'IN_PROGRESS') acc.inProgress++;
        else if (status === 'COMPLETED') acc.completed++;
        else if (status === 'EXPIRED') acc.expired++;
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

          switch (event.__typename) {
            case 'RefTestStarted':
              this.moveRefTestBetweenStatusLists(
                event.id,
                { status: event.status, startedAt: event.startedAt },
                'PENDING',
                'IN_PROGRESS',
              );
              this.updateStatusCounts('PENDING', 'IN_PROGRESS');
              break;

            case 'RefTestCompleted':
              this.moveRefTestBetweenStatusLists(
                event.id,
                {
                  status: event.status,
                  completedAt: event.completedAt,
                  questionScore: event.questionScore,
                  questionTotal: event.questionTotal,
                  answerScore: event.answerScore,
                  answerTotal: event.answerTotal,
                  percentage: event.percentage,
                  language: event.language,
                },
                'IN_PROGRESS',
                'COMPLETED',
              );
              this.updateStatusCounts('IN_PROGRESS', 'COMPLETED');
              break;

            case 'RefTestExpired':
              this.moveRefTestBetweenStatusLists(
                event.id,
                { status: event.status },
                'PENDING',
                'EXPIRED',
              );
              this.updateStatusCounts('PENDING', 'EXPIRED');
              break;

            case 'RefTestReset': {
              const oldStatus = event.oldStatus;
              this.moveRefTestBetweenStatusLists(
                event.id,
                {
                  status: 'PENDING',
                  startedAt: null,
                  completedAt: null,
                  questionScore: null,
                  questionTotal: null,
                  answerScore: null,
                  answerTotal: null,
                  percentage: null,
                  resultsSent: false,
                },
                oldStatus,
                'PENDING',
              );
              this.updateStatusCounts(oldStatus, 'PENDING');
              break;
            }

            case 'RefTestRevived':
              this.moveRefTestBetweenStatusLists(
                event.id,
                {
                  status: 'PENDING',
                  invitationSent: false,
                },
                'EXPIRED',
                'PENDING',
              );
              this.updateStatusCounts('EXPIRED', 'PENDING');
              break;

            case 'RefTestCreated': {
              // We now receive the full node data in the subscription event, so we can
              // insert the new item directly into the ALL and PENDING cached lists
              // without evicting or triggering any network request.
              const createdEvent = event;
              const node = {
                id: createdEvent.id,
                name: createdEvent.name,
                email: createdEvent.email,
                title:
                  createdEvent.titleId != null
                    ? {
                        __typename: 'RefTestTitleDto',
                        id: createdEvent.titleId,
                        value: createdEvent.titleValue ?? '',
                      }
                    : null,
                invitationSent: createdEvent.invitationSent,
                resultsSent: createdEvent.resultsSent,
                sendInvitationsAutomatically: createdEvent.sendInvitationsAutomatically,
                sendResultsAutomatically: createdEvent.sendResultsAutomatically,
                status: createdEvent.status,
                numberOfQuestions: createdEvent.numberOfQuestions,
                maxTimeInMinutes: createdEvent.maxTimeInMinutes,
                startedAt: null,
                completedAt: null,
                questionScore: null,
                answerScore: null,
                questionTotal: null,
                answerTotal: null,
                percentage: null,
              };
              // Add to PENDING list (the new test is always PENDING)
              this.addEdgeToCachedList(node as any, '', 'PENDING');
              // Add to ALL list (undefined = no status filter)
              this.addEdgeToCachedList(node as any, '', undefined);
              // Increment tab counters
              this.updateStatusCounts(undefined, 'PENDING', true);
              break;
            }

            case 'RefTestDeleted': {
              // If this client initiated the delete, the mutation update callback is the
              // sole handler (regardless of SSE/response ordering). Skip here to avoid
              // a double-decrement when the SSE arrives before the mutation response.
              if (this._pendingDeleteIds.has(event.id)) break;

              // Remote delete — remove from cache and decrement counts only if the
              // item was actually present (avoids processing the same event twice).
              const removed = this.removeFromAllCachedStatusLists([
                { id: event.id, status: event.status },
              ]);
              if (removed > 0) {
                this.decrementCountsForDelete(event.status);
              }
              break;
            }

            case 'RefTestInvitationSent': {
              const entityId = this._apollo.client.cache.identify({
                __typename: 'RefTest',
                id: event.id,
              });
              if (entityId)
                this._apollo.client.cache.modify({
                  id: entityId,
                  fields: { invitationSent: () => true },
                });
              break;
            }

            case 'RefTestResultSent': {
              const entityId = this._apollo.client.cache.identify({
                __typename: 'RefTest',
                id: event.id,
              });
              if (entityId)
                this._apollo.client.cache.modify({
                  id: entityId,
                  fields: { resultsSent: () => true },
                });
              break;
            }
          }
        }),
        catchError(() => EMPTY),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe();
  }

  /* ------------------------------------------------------------------------ */
  /* Cache Helpers — List Manipulation                                        */
  /* ------------------------------------------------------------------------ */

  private buildListVariables(status: RefTestStatus | undefined) {
    const filter = this._filterState.filter();
    const base = this._queryBuilder.buildWhereFilter(filter, { excludeStatus: true });
    const where = status ? this._queryBuilder.mergeFilters(base, { status: { eq: status } }) : base;
    return {
      first: filter.pagingInfo.first,
      where,
      order: this._queryBuilder.buildOrderClause(filter),
    };
  }

  private readCachedList(status: RefTestStatus | undefined): GetRefTestsQuery | null {
    return (this._apollo.client.cache as ApolloCache).readQuery<GetRefTestsQuery>({
      query: this._getRefTestsGQL.document,
      variables: this.buildListVariables(status),
    });
  }

  private writeCachedList(status: RefTestStatus | undefined, data: GetRefTestsQuery): void {
    (this._apollo.client.cache as ApolloCache).writeQuery({
      query: this._getRefTestsGQL.document,
      variables: this.buildListVariables(status),
      data,
    });
  }

  /**
   * Removes edges by ID from a specific status-filtered cached list (or the All list when status is undefined).
   * Also updates totalCount. Returns the number of edges actually removed.
   */
  private removeEdgesFromCachedList(
    ids: ReadonlySet<string>,
    status: RefTestStatus | undefined,
  ): number {
    const prev = this.readCachedList(status);
    if (!prev?.refTests?.edges) return 0;

    const newEdges = prev.refTests.edges.filter((e) => !ids.has(e?.node?.id ?? ''));
    const removed = prev.refTests.edges.length - newEdges.length;
    if (removed === 0) return 0;

    this.writeCachedList(status, {
      ...prev,
      refTests: {
        ...prev.refTests,
        edges: newEdges,
        totalCount: prev.refTests.totalCount - removed,
      },
    });
    return removed;
  }

  /**
   * Removes deleted items from the All list and each status-specific list.
   * Returns the total number of edges actually removed across all lists.
   */
  private removeFromAllCachedStatusLists(
    deletedItems: ReadonlyArray<{ id: string; status: RefTestStatus }>,
  ): number {
    const allIds = new Set(deletedItems.map((d) => d.id));
    let totalRemoved = 0;

    // Remove from All list (no status filter)
    totalRemoved += this.removeEdgesFromCachedList(allIds, undefined);

    // Remove from each relevant status-specific list
    const byStatus = new Map<RefTestStatus, Set<string>>();
    for (const { id, status } of deletedItems) {
      const set = byStatus.get(status) ?? new Set<string>();
      set.add(id);
      byStatus.set(status, set);
    }
    for (const [status, ids] of byStatus) {
      totalRemoved += this.removeEdgesFromCachedList(ids, status);
    }

    return totalRemoved;
  }

  /**
   * Removes a single edge from a specific cached list (or All list) and decrements totalCount.
   */
  private removeEdgeFromCachedList(id: string, status: RefTestStatus | undefined): void {
    this.removeEdgesFromCachedList(new Set([id]), status);
  }

  /**
   * Appends an updated edge to a cached list if the list exists in cache and the item isn't already there.
   * Also increments totalCount.
   */
  private addEdgeToCachedList(
    updatedNode: NonNullable<
      NonNullable<NonNullable<GetRefTestsQuery['refTests']>['edges']>[number]
    >['node'],
    cursor: string,
    status: RefTestStatus | undefined,
  ): void {
    const prev = this.readCachedList(status);
    if (!prev?.refTests) return;

    // Don't add if already present
    if (prev.refTests.edges?.some((e) => e?.node?.id === updatedNode?.id)) return;

    this.writeCachedList(status, {
      ...prev,
      refTests: {
        ...prev.refTests,
        edges: [...(prev.refTests.edges ?? []), { cursor, node: updatedNode }],
        totalCount: prev.refTests.totalCount + 1,
      },
    });
  }

  /**
   * Finds an edge (node + cursor) from the first cached list that contains it.
   * Tries statuses in the given order; undefined means the All list.
   */
  private findEdgeInCache(
    id: string,
    ...statuses: Array<RefTestStatus | undefined>
  ): {
    node: NonNullable<
      NonNullable<NonNullable<GetRefTestsQuery['refTests']>['edges']>[number]
    >['node'];
    cursor: string;
  } | null {
    for (const status of statuses) {
      const data = this.readCachedList(status);
      const edge = data?.refTests?.edges?.find((e) => e?.node?.id === id);
      if (edge?.node) return { node: edge.node, cursor: edge.cursor ?? '' };
    }
    return null;
  }

  /**
   * Moves a ref test from one status list to another, updating both lists and the entity globally.
   * Also updates the All list in-place (entity update propagates automatically via normalization).
   */
  private moveRefTestBetweenStatusLists(
    id: string,
    updates: Record<string, unknown>,
    oldStatus: RefTestStatus,
    newStatus: RefTestStatus,
  ): void {
    // Find the edge BEFORE removing it so we still have the node data
    const found = this.findEdgeInCache(id, oldStatus, undefined);

    // Update the normalized entity so the All list reflects the new state immediately
    const entityId = this._apollo.client.cache.identify({ __typename: 'RefTest', id });
    if (entityId) {
      const fields: Record<string, () => unknown> = {};
      for (const [k, v] of Object.entries(updates)) {
        fields[k] = () => v;
      }
      this._apollo.client.cache.modify({ id: entityId, fields });
    }

    // Remove edge from old-status list
    this.removeEdgeFromCachedList(id, oldStatus);

    // Add updated edge to new-status list
    if (found?.node) {
      const updatedNode = { ...found.node, ...updates } as typeof found.node;
      this.addEdgeToCachedList(updatedNode, found.cursor, newStatus);
    }
  }

  /* ------------------------------------------------------------------------ */
  /* Cache Helpers — Status Counts                                            */
  /* ------------------------------------------------------------------------ */

  private updateStatusCounts(
    oldStatus: RefTestStatus | undefined,
    newStatus: RefTestStatus,
    incrementAll = false,
  ): void {
    const current = this._countsQueryRef.getCurrentResult();
    if (!current?.data) return;

    // @ts-expect-error Apollo typing
    this._countsQueryRef.updateQuery((prev) => {
      if (!prev) return prev;

      const result = { ...prev };

      // Optionally increment the 'all' count (used for new creations)
      if (incrementAll) {
        result.all = increment(result.all);
      }

      // Decrement old status
      switch (oldStatus) {
        case 'PENDING':
          result.pending = decrement(result.pending);
          break;
        case 'IN_PROGRESS':
          result.inProgress = decrement(result.inProgress);
          break;
        case 'COMPLETED':
          result.completed = decrement(result.completed);
          break;
        case 'EXPIRED':
          result.expired = decrement(result.expired);
          break;
      }

      // Increment new status
      switch (newStatus) {
        case 'PENDING':
          result.pending = increment(result.pending);
          break;
        case 'IN_PROGRESS':
          result.inProgress = increment(result.inProgress);
          break;
        case 'COMPLETED':
          result.completed = increment(result.completed);
          break;
        case 'EXPIRED':
          result.expired = increment(result.expired);
          break;
      }

      return result;
    });
  }

  private decrementCountsForDelete(status: RefTestStatus): void {
    const current = this._countsQueryRef.getCurrentResult();
    if (!current?.data) return;

    // @ts-expect-error Apollo typing
    this._countsQueryRef.updateQuery((prev) => {
      if (!prev) return prev;

      const result = { ...prev };

      result.all = decrement(result.all);

      switch (status) {
        case 'PENDING':
          result.pending = decrement(result.pending);
          break;
        case 'IN_PROGRESS':
          result.inProgress = decrement(result.inProgress);
          break;
        case 'COMPLETED':
          result.completed = decrement(result.completed);
          break;
        case 'EXPIRED':
          result.expired = decrement(result.expired);
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
        status: { eq: 'PENDING' },
      }),
      inProgressWhere: this._queryBuilder.mergeFilters(base, {
        status: { eq: 'IN_PROGRESS' },
      }),
      completedWhere: this._queryBuilder.mergeFilters(base, {
        status: { eq: 'COMPLETED' },
      }),
      expiredWhere: this._queryBuilder.mergeFilters(base, {
        status: { eq: 'EXPIRED' },
      }),
    };
  }
}
