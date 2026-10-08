import { computed, DestroyRef, ErrorHandler, inject, Service, Signal, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ApolloClient } from '@apollo/client';
import { Apollo } from 'apollo-angular';
import {
  catchError,
  combineLatest,
  EMPTY,
  map,
  Observable,
  startWith,
  Subject,
  switchMap,
  tap,
} from 'rxjs';
import {
  ApproveRefTestsGQL,
  DeleteRefTestsGQL,
  GetRefTestsAllCountsGQL,
  GetRefTestsGQL,
  GetRefTestsQuery,
  RefTestResetType,
  RefTestsUpdatedGQL,
  RejectRefTestsGQL,
  ResetRefTestsGQL,
  ReviveRefTestsGQL,
  SendRefTestInvitationsGQL,
  SendRefTestResultsGQL,
  SendReportGQL,
} from '../../../../graphql/generated';
import { MutationCallbacks, runMutation } from '../../shared/utils/apollo-utils';
import { REF_TEST_CONFIG } from '../list/services/constants';
import { RefTestFilterState } from '../list/services/ref-test-filter-state';
import { RefTestQueryBuilder } from '../list/services/ref-test-query-builder';
import { IRefTestFilter, IReportResult } from '../list/services/types';
import { RefTestCacheUpdater } from './ref-test-cache-updater';

/* -------------------------------------------------------------------------- */
/* Service                                                                    */
/* -------------------------------------------------------------------------- */

/** Facade for RefTest list state, queries, pagination, and mutations. */
@Service()
export class RefTestData {
  private readonly _apollo = inject(Apollo);
  private readonly _errorHandler = inject(ErrorHandler);
  private readonly _getRefTestsGQL = inject(GetRefTestsGQL);
  private readonly _cacheUpdater = inject(RefTestCacheUpdater);
  private readonly _getRefTestsAllCountsGQL = inject(GetRefTestsAllCountsGQL);
  private readonly _deleteRefTestsGQL = inject(DeleteRefTestsGQL);
  private readonly _sendInvitationsGQL = inject(SendRefTestInvitationsGQL);
  private readonly _sendResultsGQL = inject(SendRefTestResultsGQL);
  private readonly _sendReportGQL = inject(SendReportGQL);
  private readonly _resetRefTestsGQL = inject(ResetRefTestsGQL);
  private readonly _reviveRefTestsGQL = inject(ReviveRefTestsGQL);
  private readonly _approveRefTestsGQL = inject(ApproveRefTestsGQL);
  private readonly _rejectRefTestsGQL = inject(RejectRefTestsGQL);
  private readonly _refTestsUpdatedGQL = inject(RefTestsUpdatedGQL);
  private readonly _filterState = inject(RefTestFilterState);
  private readonly _queryBuilder = inject(RefTestQueryBuilder);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _queryError = signal<unknown | null>(null);
  private readonly _queryLoading = signal(true);
  private readonly _queryRefresh = new Subject<void>();

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
    notifyOnNetworkStatusChange: true,
  });

  private readonly _countsQueryRef = this._getRefTestsAllCountsGQL.watch({
    variables: this._cacheUpdater.buildCountsVariables(),
    notifyOnNetworkStatusChange: true,
  });
  private readonly _lastQueryData = signal(this._queryRef.getCurrentResult().data);
  private _activeFilter: IRefTestFilter = this._filterState.filter();

  constructor() {
    this._cacheUpdater.refreshRequests
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((refresh) => this.refreshActiveQueries(refresh));
  }

  private readonly _queryResult = toSignal(
    combineLatest([
      toObservable(this._filterState.filter),
      this._queryRefresh.pipe(startWith(undefined)),
    ]).pipe(
      switchMap(([filter]) => {
        if (this._activeFilter !== filter) {
          this._activeFilter = filter;
          this._lastQueryData.set(undefined);
          this._queryLoading.set(true);
        }
        this._queryRef.setVariables({
          first: filter.pagingInfo.first,
          after: filter.pagingInfo.after,
          where: this._queryBuilder.buildWhereFilter(filter),
          order: this._queryBuilder.buildOrderClause(filter),
        });
        return this._queryRef.valueChanges.pipe(
          tap((result) => {
            if (result.data) this._lastQueryData.set(result.data);
            this._queryError.set(result.error ?? null);
            this._queryLoading.set(result.loading);
          }),
          tap((result) => this._cacheUpdater.setListQueryLoading(result.loading)),
          tap(() => this._cacheUpdater.replayPendingEvents()),
          catchError((error: unknown) => {
            this._queryError.set(error);
            this._queryLoading.set(false);
            this._cacheUpdater.setListQueryLoading(false);
            return EMPTY;
          }),
        );
      }),
    ),
  );

  readonly queryResult = computed(() => {
    const result = this._queryResult();
    const lastData = this._lastQueryData();
    return result && !result.data && lastData ? { ...result, data: lastData } : result;
  });

  readonly queryError = this._queryError.asReadonly();

  readonly loading = computed(() => !this.queryError() && this._queryLoading());

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
        this._countsQueryRef.setVariables(this._cacheUpdater.buildCountsVariables());
        return this._countsQueryRef.valueChanges.pipe(
          tap((result) => this._cacheUpdater.setCountsQueryLoading(result.loading)),
          tap(() => this._cacheUpdater.replayPendingEvents()),
        );
      }),
      map((r) => ({
        all: r.data?.all?.totalCount ?? 0,
        pending: r.data?.pending?.totalCount ?? 0,
        inProgress: r.data?.inProgress?.totalCount ?? 0,
        completed: r.data?.completed?.totalCount ?? 0,
        expired: r.data?.expired?.totalCount ?? 0,
        pendingApproval: r.data?.pendingApproval?.totalCount ?? 0,
        rejected: r.data?.rejected?.totalCount ?? 0,
      })),
    ),
    {
      initialValue: {
        all: 0,
        pending: 0,
        inProgress: 0,
        completed: 0,
        expired: 0,
        pendingApproval: 0,
        rejected: 0,
      },
    },
  );

  /* ------------------------------------------------------------------------ */
  /* Pagination / Reset                                                       */
  /* ------------------------------------------------------------------------ */

  fetchMore(): Observable<ApolloClient.QueryResult<GetRefTestsQuery>> {
    return this._cacheUpdater.trackListRead(() =>
      this._queryRef.fetchMore({
        variables: {
          first: this._filterState.filter().pagingInfo.first,
          after: this.queryResult()?.data?.refTests?.pageInfo?.endCursor,
          where: this._queryBuilder.buildWhereFilter(this._filterState.filter()),
          order: this._queryBuilder.buildOrderClause(this._filterState.filter()),
        },
      }),
    );
  }

  reset(): void {
    // Evict all cached refTests entries (all variable combinations / status tabs)
    // so every tab fetches fresh data on next visit.
    this._apollo.client.cache.evict({ id: 'ROOT_QUERY', fieldName: 'refTests' });
    this._apollo.client.cache.gc();
    this.refreshActiveQueries({ lists: true, counts: true });
  }

  retry(): void {
    this.refreshActiveQueries({ lists: true, counts: true });
  }

  private refreshActiveQueries(refresh: { lists: boolean; counts: boolean }): void {
    if (refresh.lists) {
      this._queryLoading.set(true);
      this._queryRefresh.next();
      this._cacheUpdater.trackListRead(() => this._queryRef.refetch()).subscribe({
        error: (error: unknown) => {
          this._queryError.set(error);
          this._errorHandler.handleError(error);
        },
      });
    }
    if (refresh.counts) {
      this._cacheUpdater.trackCountsRead(() => this._countsQueryRef.refetch()).subscribe({
        error: (error: unknown) => this._errorHandler.handleError(error),
      });
    }
  }

  private syncQueryLoadingState(): void {
    this._cacheUpdater.setListQueryLoading(this._queryRef.getCurrentResult().loading);
    this._cacheUpdater.setCountsQueryLoading(this._countsQueryRef.getCurrentResult().loading);
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
    this._cacheUpdater.markPendingDeletes(ids);
    const deleteCallbacks: MutationCallbacks<string[]> = {
      ...callbacks,
      onError: (error) => {
        this._cacheUpdater.clearPendingDeletes(ids);
        callbacks.onError?.(error);
      },
      onComplete: () => {
        this._cacheUpdater.clearPendingDeletes(ids);
        callbacks.onComplete?.();
      },
    };
    return runMutation(
      this._deleteRefTestsGQL.mutate({
        variables: { input: { ids } },
        update: this._cacheUpdater.updateDeleteCache.bind(this._cacheUpdater),
      }),
      destroyRef,
      deleteCallbacks,
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
    const transition = this._cacheUpdater.beginTransition('RefTestReset', input.ids);
    return runMutation(
      this._resetRefTestsGQL.mutate({
        variables: { input },
        update: (_, { data }) =>
          transition.confirm(data?.resetRefTests?.resetRefTestsResult?.resetRefTests),
      }),
      destroyRef,
      transition.cancelOnError(callbacks),
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
    const transition = this._cacheUpdater.beginTransition('RefTestRevived', ids);
    return runMutation(
      this._reviveRefTestsGQL.mutate({
        variables: { input: { ids } },
        update: (_, { data }) =>
          transition.confirm(data?.reviveRefTests?.reviveRefTestsResult?.revivedRefTests),
      }),
      destroyRef,
      transition.cancelOnError(callbacks),
      (r) => {
        const res = r.data?.reviveRefTests?.reviveRefTestsResult;
        return {
          successCount: res?.successfullyRevived ?? 0,
          failedCount: res?.failed ?? 0,
        };
      },
    );
  }

  approveRefTests(
    ids: string[],
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<{ successCount: number; failedCount: number }> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    const transition = this._cacheUpdater.beginTransition('RefTestApproved', ids);
    return runMutation(
      this._approveRefTestsGQL.mutate({
        variables: { input: { ids } },
        update: (_, { data }) =>
          transition.confirm(data?.approveRefTests?.approveRefTestsResult?.approvedRefTests),
      }),
      destroyRef,
      transition.cancelOnError(callbacks),
      (r) => {
        const res = r.data?.approveRefTests?.approveRefTestsResult;
        return {
          successCount: res?.successfullyApproved ?? 0,
          failedCount: res?.failed ?? 0,
        };
      },
    );
  }

  rejectRefTests(
    ids: string[],
    reason: string,
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<{ successCount: number; failedCount: number }> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    const transition = this._cacheUpdater.beginTransition('RefTestRejected', ids);
    return runMutation(
      this._rejectRefTestsGQL.mutate({
        variables: { input: { ids, reason } },
        update: (_, { data }) =>
          transition.confirm(data?.rejectRefTests?.rejectRefTestsResult?.rejectedRefTests),
      }),
      destroyRef,
      transition.cancelOnError(callbacks),
      (r) => {
        const res = r.data?.rejectRefTests?.rejectRefTestsResult;
        return {
          successCount: res?.successfullyRejected ?? 0,
          failedCount: res?.failed ?? 0,
        };
      },
    );
  }

  /* ------------------------------------------------------------------------ */
  /* List subscription                                                        */
  /* ------------------------------------------------------------------------ */

  public subscribeToRefTestUpdates(destroyRef: DestroyRef): void {
    this._refTestsUpdatedGQL
      .subscribe()
      .pipe(
        map((r) => r.data?.refTestsUpdated),
        tap((event) => {
          if (!event) return;
          this.syncQueryLoadingState();
          this._cacheUpdater.replayPendingEvents();
          this._cacheUpdater.updateCacheFromSubscription(event, undefined, 'list');
        }),
        catchError(() => EMPTY),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe();
  }
}
