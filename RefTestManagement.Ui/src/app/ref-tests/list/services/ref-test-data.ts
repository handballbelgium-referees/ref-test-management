import { computed, DestroyRef, inject, Injectable } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ApolloClient } from '@apollo/client';
import { onlyCompleteData } from 'apollo-angular';
import { catchError, EMPTY, finalize, map, of, switchMap, tap } from 'rxjs';
import {
  DeleteRefTestsGQL,
  GetRefTestsAllCountsGQL,
  GetRefTestsAllCountsQuery,
  GetRefTestsGQL,
  GetRefTestsQuery,
  RefTestStatus,
  RefTestsUpdatedGQL,
  ResetRefTestsGQL,
  ReviveRefTestsGQL,
  SendRefTestInvitationsGQL,
  SendRefTestResultsGQL,
  SendReportGQL,
} from '../../../../../graphql/generated';
import { REF_TEST_CONFIG } from './constants';
import { RefTestFilterState } from './ref-test-filter-state';
import { RefTestQueryBuilder } from './ref-test-query-builder';
import { IReportResult } from './types';

@Injectable()
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
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _filterState = inject(RefTestFilterState);
  private readonly _queryBuilder = inject(RefTestQueryBuilder);

  private readonly _queryRef = this._getRefTestsGQL.watch({
    variables: {
      first: this._filterState.filter().pagingInfo.first,
      after: this._filterState.filter().pagingInfo.after,
      where: this._queryBuilder.buildWhereFilter(this._filterState.filter()),
      order: this._queryBuilder.buildOrderClause(this._filterState.filter()),
    },
  });

  private readonly endCursor = computed(() => {
    return this.queryResult()?.data?.refTests?.pageInfo?.endCursor ?? undefined;
  });

  private readonly _countsQueryRef = this._getRefTestsAllCountsGQL.watch({
    variables: {
      allWhere: this._queryBuilder.buildWhereFilter(this._filterState.filter(), {
        excludeStatus: true,
      }),
      pendingWhere: this._queryBuilder.mergeFilters(
        this._queryBuilder.buildWhereFilter(this._filterState.filter(), {
          excludeStatus: true,
        }),
        { status: { eq: RefTestStatus.Pending } },
      ),
      inProgressWhere: this._queryBuilder.mergeFilters(
        this._queryBuilder.buildWhereFilter(this._filterState.filter(), {
          excludeStatus: true,
        }),
        { status: { eq: RefTestStatus.InProgress } },
      ),
      completedWhere: this._queryBuilder.mergeFilters(
        this._queryBuilder.buildWhereFilter(this._filterState.filter(), {
          excludeStatus: true,
        }),
        { status: { eq: RefTestStatus.Completed } },
      ),
      expiredWhere: this._queryBuilder.mergeFilters(
        this._queryBuilder.buildWhereFilter(this._filterState.filter(), {
          excludeStatus: true,
        }),
        { status: { eq: RefTestStatus.Expired } },
      ),
    },
  });

  readonly queryResult = toSignal(
    toObservable(this._filterState.filter).pipe(
      switchMap((filter) => {
        const where = this._queryBuilder.buildWhereFilter(filter);
        const order = this._queryBuilder.buildOrderClause(filter);
        this._queryRef.setVariables({
          where,
          order,
          first: filter.pagingInfo.first,
          after: filter.pagingInfo.after,
        });

        return this._queryRef.valueChanges;
      }),
    ),
  );

  readonly hasData = computed(() => {
    return (this.queryResult()?.data?.refTests?.edges?.length ?? 0) > 0;
  });

  readonly loading = computed(() => {
    return this.queryResult()?.loading ?? false;
  });

  readonly hasNextPage = computed(() => {
    return this.queryResult()?.data?.refTests?.pageInfo?.hasNextPage ?? false;
  });

  readonly showPerformanceWarning = computed(() => {
    const loadedCount = this.queryResult()?.data?.refTests?.edges?.length ?? 0;
    return (
      loadedCount >= REF_TEST_CONFIG.PERFORMANCE_WARNING_THRESHOLD &&
      loadedCount < REF_TEST_CONFIG.MAX_LOADABLE_ITEMS &&
      this.hasNextPage()
    );
  });

  readonly canLoadMore = computed(() => {
    const loadedCount = this.queryResult()?.data?.refTests?.edges?.length ?? 0;
    return this.hasNextPage() && loadedCount < REF_TEST_CONFIG.MAX_LOADABLE_ITEMS;
  });

  readonly isAtMaxCapacity = computed(() => {
    const loadedCount = this.queryResult()?.data?.refTests?.edges?.length ?? 0;
    return loadedCount >= REF_TEST_CONFIG.MAX_LOADABLE_ITEMS && this.hasNextPage();
  });

  readonly statusCounts = toSignal(
    toObservable(this._filterState.filter).pipe(
      switchMap((filter) => {
        const baseFilter = this._queryBuilder.buildWhereFilter(filter, {
          excludeStatus: true,
        });
        const variables = {
          allWhere: baseFilter,
          pendingWhere: this._queryBuilder.mergeFilters(baseFilter, {
            status: { eq: RefTestStatus.Pending },
          }),
          inProgressWhere: this._queryBuilder.mergeFilters(baseFilter, {
            status: { eq: RefTestStatus.InProgress },
          }),
          completedWhere: this._queryBuilder.mergeFilters(baseFilter, {
            status: { eq: RefTestStatus.Completed },
          }),
          expiredWhere: this._queryBuilder.mergeFilters(baseFilter, {
            status: { eq: RefTestStatus.Expired },
          }),
        };
        this._countsQueryRef.setVariables(variables);
        return this._countsQueryRef.valueChanges;
      }),
      onlyCompleteData(),
      map((result) => ({
        all: result.data?.all?.totalCount ?? 0,
        pending: result.data?.pending?.totalCount ?? 0,
        inProgress: result.data?.inProgress?.totalCount ?? 0,
        completed: result.data?.completed?.totalCount ?? 0,
        expired: result.data?.expired?.totalCount ?? 0,
      })),
    ),
    {
      initialValue: {
        all: 0,
        pending: 0,
        inProgress: 0,
        completed: 0,
        expired: 0,
      },
    },
  );

  constructor() {
    // Subscribe to all ref test updates - single subscription for all events
    this._refTestsUpdatedGQL
      .subscribe()
      .pipe(
        map((result) => result.data?.refTestsUpdated),
        tap((event) => {
          if (!event) return;

          // Only update if this ref test is currently loaded
          const currentData = this._queryRef.getCurrentResult();
          const isLoaded = currentData?.data?.refTests?.edges?.some(
            (edge) => edge?.node?.id === event.id,
          );

          if (isLoaded) {
            this.updateRefTestInCache(event.id, event);
          }
        }),
        catchError(() => {
          return EMPTY;
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  private updateRefTestInCache(id: string, updates: Record<string, unknown>): void {
    const currentData = this._queryRef.getCurrentResult();
    if (!currentData?.data?.refTests) return;

    // Track old status if we're updating the status
    let oldStatus: RefTestStatus | undefined;
    if ('status' in updates) {
      const edge = currentData.data.refTests.edges?.find((e) => e?.node?.id === id);
      oldStatus = edge?.node?.status;
    }

    // Update using updateQuery - Apollo will merge the changes
    // TypeScript doesn't like the deep partial typing from Apollo, but the update is safe
    // @ts-expect-error - Apollo's updateQuery has complex typing that doesn't match our return
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

    // Update status counts if status changed
    if ('status' in updates && oldStatus !== updates['status']) {
      this.updateStatusCounts(oldStatus, updates['status'] as RefTestStatus);
    }
  }

  private updateStatusCounts(oldStatus: RefTestStatus | undefined, newStatus: RefTestStatus): void {
    const currentCounts = this._countsQueryRef.getCurrentResult();
    if (!currentCounts?.data) return;

    // @ts-expect-error - Apollo's updateQuery has complex typing
    this._countsQueryRef.updateQuery((prev) => {
      if (!prev) return prev;

      const result = { ...prev };

      // Decrease old status count
      if (oldStatus === RefTestStatus.Pending && result.pending?.totalCount) {
        result.pending = { ...result.pending, totalCount: result.pending.totalCount - 1 };
      } else if (oldStatus === RefTestStatus.InProgress && result.inProgress?.totalCount) {
        result.inProgress = {
          ...result.inProgress,
          totalCount: result.inProgress.totalCount - 1,
        };
      } else if (oldStatus === RefTestStatus.Completed && result.completed?.totalCount) {
        result.completed = { ...result.completed, totalCount: result.completed.totalCount - 1 };
      } else if (oldStatus === RefTestStatus.Expired && result.expired?.totalCount) {
        result.expired = { ...result.expired, totalCount: result.expired.totalCount - 1 };
      }

      // Increase new status count
      if (newStatus === RefTestStatus.Pending && result.pending?.totalCount !== undefined) {
        result.pending = { ...result.pending, totalCount: result.pending.totalCount + 1 };
      } else if (
        newStatus === RefTestStatus.InProgress &&
        result.inProgress?.totalCount !== undefined
      ) {
        result.inProgress = {
          ...result.inProgress,
          totalCount: result.inProgress.totalCount + 1,
        };
      } else if (
        newStatus === RefTestStatus.Completed &&
        result.completed?.totalCount !== undefined
      ) {
        result.completed = { ...result.completed, totalCount: result.completed.totalCount + 1 };
      } else if (newStatus === RefTestStatus.Expired && result.expired?.totalCount !== undefined) {
        result.expired = { ...result.expired, totalCount: result.expired.totalCount + 1 };
      }

      return result;
    });
  }

  fetchMore(): Promise<ApolloClient.QueryResult<GetRefTestsQuery>> {
    return this._queryRef.fetchMore({
      variables: {
        first: this._filterState.filter().pagingInfo.first,
        after: this.endCursor(),
        where: this._queryBuilder.buildWhereFilter(this._filterState.filter()),
        order: this._queryBuilder.buildOrderClause(this._filterState.filter()),
      },
    });
  }

  reset(): void {
    this._queryRef.refetch({
      first: this._filterState.filter().pagingInfo.first,
      after: this._filterState.filter().pagingInfo.after,
      where: this._queryBuilder.buildWhereFilter(this._filterState.filter()),
      order: this._queryBuilder.buildOrderClause(this._filterState.filter()),
    });

    this._countsQueryRef.refetch({
      allWhere: this._queryBuilder.buildWhereFilter(this._filterState.filter(), {
        excludeStatus: true,
      }),
      pendingWhere: this._queryBuilder.mergeFilters(
        this._queryBuilder.buildWhereFilter(this._filterState.filter(), {
          excludeStatus: true,
        }),
        { status: { eq: RefTestStatus.Pending } },
      ),
      inProgressWhere: this._queryBuilder.mergeFilters(
        this._queryBuilder.buildWhereFilter(this._filterState.filter(), {
          excludeStatus: true,
        }),
        { status: { eq: RefTestStatus.InProgress } },
      ),
      completedWhere: this._queryBuilder.mergeFilters(
        this._queryBuilder.buildWhereFilter(this._filterState.filter(), {
          excludeStatus: true,
        }),
        { status: { eq: RefTestStatus.Completed } },
      ),
      expiredWhere: this._queryBuilder.mergeFilters(
        this._queryBuilder.buildWhereFilter(this._filterState.filter(), {
          excludeStatus: true,
        }),
        { status: { eq: RefTestStatus.Expired } },
      ),
    });
  }

  deleteRefTests(
    ids: string[],
    callbacks: {
      onStart?: () => void;
      onSuccess?: (deletedIds: string[]) => void;
      onError?: (error: any) => void;
      onComplete?: () => void;
    } = {},
  ): void {
    if (callbacks.onStart) callbacks.onStart();

    this._deleteRefTestsGQL
      .mutate({
        variables: { input: { ids } },
        update: (cache, { data }) => {
          const refTestCounts = cache.readQuery<GetRefTestsAllCountsQuery>({
            query: this._getRefTestsAllCountsGQL.document,
            variables: this._countsQueryRef.variables,
          });

          if (!refTestCounts) return;

          const deletedRefTests = data?.deleteRefTests.deleteRefTestsResult?.deletedRefTests ?? [];

          // Count deletions by status
          const deletionCounts = {
            total: deletedRefTests.length,
            pending: 0,
            inProgress: 0,
            completed: 0,
            expired: 0,
          };

          for (const deleted of deletedRefTests) {
            switch (deleted.status) {
              case RefTestStatus.Pending:
                deletionCounts.pending++;
                break;
              case RefTestStatus.InProgress:
                deletionCounts.inProgress++;
                break;
              case RefTestStatus.Completed:
                deletionCounts.completed++;
                break;
              case RefTestStatus.Expired:
                deletionCounts.expired++;
                break;
            }
          }

          // Create new immutable counts object
          const updatedCounts: GetRefTestsAllCountsQuery = {
            all: refTestCounts.all
              ? {
                  ...refTestCounts.all,
                  totalCount: refTestCounts.all.totalCount - deletionCounts.total,
                }
              : refTestCounts.all,
            pending: refTestCounts.pending
              ? {
                  ...refTestCounts.pending,
                  totalCount: refTestCounts.pending.totalCount - deletionCounts.pending,
                }
              : refTestCounts.pending,
            inProgress: refTestCounts.inProgress
              ? {
                  ...refTestCounts.inProgress,
                  totalCount: refTestCounts.inProgress.totalCount - deletionCounts.inProgress,
                }
              : refTestCounts.inProgress,
            completed: refTestCounts.completed
              ? {
                  ...refTestCounts.completed,
                  totalCount: refTestCounts.completed.totalCount - deletionCounts.completed,
                }
              : refTestCounts.completed,
            expired: refTestCounts.expired
              ? {
                  ...refTestCounts.expired,
                  totalCount: refTestCounts.expired.totalCount - deletionCounts.expired,
                }
              : refTestCounts.expired,
          };

          cache.writeQuery<GetRefTestsAllCountsQuery>({
            query: this._getRefTestsAllCountsGQL.document,
            data: updatedCounts,
            variables: this._countsQueryRef.variables,
          });
        },
      })
      .pipe(
        tap((result) => {
          const deleteResult = result.data?.deleteRefTests?.deleteRefTestsResult;
          if (deleteResult && callbacks.onSuccess) {
            const deletedIds = deleteResult.deletedRefTests.map((s) => s.id);
            callbacks.onSuccess(deletedIds);
          }
        }),
        catchError((error) => {
          if (callbacks.onError) callbacks.onError(error);
          return of(null);
        }),
        finalize(() => {
          if (callbacks.onComplete) callbacks.onComplete();
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  sendInvitations(
    ids: string[],
    callbacks: {
      onStart?: () => void;
      onSuccess?: (sentIds: string[]) => void;
      onError?: (error: any) => void;
      onComplete?: () => void;
    } = {},
  ): void {
    if (callbacks.onStart) callbacks.onStart();

    this._sendInvitationsGQL
      .mutate({
        variables: { input: { ids } },
      })
      .pipe(
        tap((result) => {
          const sendResult = result.data?.sendInvitations?.sendInvitationsResult;
          if (sendResult && callbacks.onSuccess) {
            const sentIds = sendResult.sentRefTests.map((s) => s.id);
            callbacks.onSuccess(sentIds);
          }
        }),
        catchError((error) => {
          if (callbacks.onError) callbacks.onError(error);
          return of(null);
        }),
        finalize(() => {
          if (callbacks.onComplete) callbacks.onComplete();
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  sendResults(
    ids: string[],
    callbacks: {
      onStart?: () => void;
      onSuccess?: (sentIds: string[]) => void;
      onError?: (error: any) => void;
      onComplete?: () => void;
    } = {},
  ): void {
    if (callbacks.onStart) callbacks.onStart();

    this._sendResultsGQL
      .mutate({
        variables: { input: { ids } },
      })
      .pipe(
        tap((result) => {
          const sendResult = result.data?.sendResults?.sendResultsResult;
          if (sendResult && callbacks.onSuccess) {
            const sentIds = sendResult.sentRefTests.map((s) => s.id);
            callbacks.onSuccess(sentIds);
          }
        }),
        catchError((error) => {
          if (callbacks.onError) callbacks.onError(error);
          return of(null);
        }),
        finalize(() => {
          if (callbacks.onComplete) callbacks.onComplete();
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  generateReport(
    ids: string[],
    callbacks: {
      onStart?: () => void;
      onSuccess?: (result: IReportResult) => void;
      onError?: (error: any) => void;
      onComplete?: () => void;
    } = {},
  ): void {
    if (callbacks.onStart) callbacks.onStart();

    this._sendReportGQL
      .mutate({
        variables: { input: { ids } },
      })
      .pipe(
        tap((result) => {
          const sendResult = result.data?.sendReport?.sendReportResult;
          if (sendResult && callbacks.onSuccess) {
            callbacks.onSuccess({
              success: sendResult.success,
              refTestCount: sendResult.refTestCount || 0,
            });
          }
        }),
        catchError((error) => {
          if (callbacks.onError) callbacks.onError(error);
          return of(null);
        }),
        finalize(() => {
          if (callbacks.onComplete) callbacks.onComplete();
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  resetRefTests(
    ids: string[],
    resetType: any,
    regenerateToken: boolean,
    callbacks: {
      onStart?: () => void;
      onSuccess?: (successCount: number, failedCount: number) => void;
      onError?: (error: any) => void;
      onComplete?: () => void;
    } = {},
  ): void {
    if (callbacks.onStart) callbacks.onStart();

    this._resetRefTestsGQL
      .mutate({
        variables: {
          input: {
            ids,
            resetType,
            regenerateToken,
          },
        },
      })
      .pipe(
        tap((result) => {
          const resetResult = result.data?.resetRefTests?.resetRefTestsResult;
          if (resetResult && callbacks.onSuccess) {
            callbacks.onSuccess(resetResult.successfullyReset, resetResult.failed);
          }
        }),
        catchError((error) => {
          if (callbacks.onError) callbacks.onError(error);
          return of(null);
        }),
        finalize(() => {
          if (callbacks.onComplete) callbacks.onComplete();
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  reviveRefTests(
    ids: string[],
    callbacks: {
      onStart?: () => void;
      onSuccess?: (successCount: number, failedCount: number) => void;
      onError?: (error: any) => void;
      onComplete?: () => void;
    } = {},
  ): void {
    if (callbacks.onStart) callbacks.onStart();

    this._reviveRefTestsGQL
      .mutate({
        variables: {
          input: {
            ids,
          },
        },
      })
      .pipe(
        tap((result) => {
          const reviveResult = result.data?.reviveRefTests?.reviveRefTestsResult;
          if (reviveResult && callbacks.onSuccess) {
            callbacks.onSuccess(reviveResult.successfullyRevived, reviveResult.failed);
          }
        }),
        catchError((error) => {
          if (callbacks.onError) callbacks.onError(error);
          return of(null);
        }),
        finalize(() => {
          if (callbacks.onComplete) callbacks.onComplete();
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }
}
