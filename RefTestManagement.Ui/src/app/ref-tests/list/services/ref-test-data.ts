import { DestroyRef, inject, Injectable } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ApolloClient } from '@apollo/client';
import { catchError, finalize, map, of, tap } from 'rxjs';
import {
  DeleteRefTestsGQL,
  GenerateReportGQL,
  GetRefTestsAllCountsGQL,
  GetRefTestsGQL,
  GetRefTestsQuery,
  RefTestFilterInput,
  RefTestSortInput,
  RefTestStatus,
  SendRefTestInvitationsGQL,
  SendRefTestResultsGQL,
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
  private readonly _generateReportGQL = inject(GenerateReportGQL);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _filterState = inject(RefTestFilterState);
  private readonly _queryBuilder = inject(RefTestQueryBuilder);

  private readonly _queryRef = this._getRefTestsGQL.watch({
    variables: this.getInitialQueryVariables(),
    fetchPolicy: 'cache-first',
    notifyOnNetworkStatusChange: true,
  });

  private readonly _countsQueryRef = this._getRefTestsAllCountsGQL.watch({
    variables: this.getInitialCountsVariables(),
    fetchPolicy: 'cache-first',
    notifyOnNetworkStatusChange: true,
  });

  private getInitialQueryVariables() {
    const filter = this._filterState.filter();
    const where = this._queryBuilder.buildWhereFilter(filter);
    const order = this._queryBuilder.buildOrderClause(filter);
    return {
      first: REF_TEST_CONFIG.PAGE_SIZE,
      after: undefined,
      where,
      order,
    };
  }

  private getInitialCountsVariables() {
    const filter = this._filterState.filter();
    const baseFilter = this._queryBuilder.buildWhereFilter(filter, {
      excludeStatus: true,
    });

    return {
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
  }

  readonly loading = toSignal(this._queryRef.valueChanges.pipe(map((result) => result.loading)), {
    initialValue: true,
  });

  readonly queryResult = toSignal(this._queryRef.valueChanges);

  readonly statusCounts = toSignal(
    this._countsQueryRef.valueChanges.pipe(
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

  refetchRefTests(variables: {
    first: number;
    after?: string;
    where?: RefTestFilterInput;
    order?: RefTestSortInput[];
  }): void {
    this._queryRef.setVariables(variables);
  }

  fetchMore(variables: {
    first: number;
    after?: string;
    where?: RefTestFilterInput;
    order?: RefTestSortInput[];
  }): Promise<ApolloClient.QueryResult<GetRefTestsQuery>> {
    return this._queryRef.fetchMore({ variables });
  }

  updateCountQueries(): void {
    const filter = this._filterState.filter();
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
        fetchPolicy: 'no-cache',
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

    this._generateReportGQL
      .mutate({
        variables: { input: { ids } },
      })
      .pipe(
        tap((result) => {
          const generateResult = result.data?.generateRefTestsReport?.generateReportResult;
          if (generateResult && callbacks.onSuccess) {
            callbacks.onSuccess({
              success: generateResult.success,
              refTestCount: generateResult.refTestCount || 0,
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
}
