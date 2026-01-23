import { DestroyRef, inject, Injectable } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { catchError, finalize, map, of, tap } from 'rxjs';
import {
  DeleteRefTestsGQL,
  GenerateReportGQL,
  GetRefTestsAllCountsGQL,
  GetRefTestsGQL,
  RefTestFilterInput,
  RefTestStatus,
  SendRefTestInvitationsGQL,
  SendRefTestResultsGQL,
} from '../../../../../graphql/generated';
import { RefTestFilterState } from './ref-test-filter-state';
import { RefTestQueryBuilder } from './ref-test-query-builder';
import { IReportResult } from './types';

@Injectable()
export class RefTestData {
  private readonly getRefTestsGQL = inject(GetRefTestsGQL);
  private readonly getRefTestsAllCountsGQL = inject(GetRefTestsAllCountsGQL);
  private readonly deleteRefTestsGQL = inject(DeleteRefTestsGQL);
  private readonly sendInvitationsGQL = inject(SendRefTestInvitationsGQL);
  private readonly sendResultsGQL = inject(SendRefTestResultsGQL);
  private readonly generateReportGQL = inject(GenerateReportGQL);
  private readonly destroyRef = inject(DestroyRef);
  private readonly filterState = inject(RefTestFilterState);
  private readonly queryBuilder = inject(RefTestQueryBuilder);

  private readonly queryRef = this.getRefTestsGQL.watch({
    variables: this.getInitialQueryVariables(),
    fetchPolicy: 'cache-first',
    notifyOnNetworkStatusChange: true,
  });

  private readonly countsQueryRef = this.getRefTestsAllCountsGQL.watch({
    variables: this.getInitialCountsVariables(),
    fetchPolicy: 'cache-first',
    notifyOnNetworkStatusChange: true,
  });

  private getInitialQueryVariables() {
    const filter = this.filterState.filter();
    const where = this.queryBuilder.buildWhereFilter(filter);
    const order = this.queryBuilder.buildOrderClause(filter);
    return {
      first: 20, // REF_TEST_CONFIG.PAGE_SIZE
      after: undefined,
      where,
      order,
    };
  }

  private getInitialCountsVariables() {
    const filter = this.filterState.filter();
    const baseFilter = this.queryBuilder.buildWhereFilter(filter, {
      excludeStatus: true,
    });

    return {
      allWhere: baseFilter,
      pendingWhere: this.queryBuilder.mergeFilters(baseFilter, {
        status: { eq: RefTestStatus.Pending },
      }),
      inProgressWhere: this.queryBuilder.mergeFilters(baseFilter, {
        status: { eq: RefTestStatus.InProgress },
      }),
      completedWhere: this.queryBuilder.mergeFilters(baseFilter, {
        status: { eq: RefTestStatus.Completed },
      }),
      expiredWhere: this.queryBuilder.mergeFilters(baseFilter, {
        status: { eq: RefTestStatus.Expired },
      }),
    };
  }

  readonly loading = toSignal(this.queryRef.valueChanges.pipe(map((result) => result.loading)), {
    initialValue: true,
  });

  readonly queryResult = toSignal(this.queryRef.valueChanges);

  readonly statusCounts = toSignal(
    this.countsQueryRef.valueChanges.pipe(
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
    order?: any;
  }): void {
    this.queryRef.refetch(variables);
  }

  fetchMore(variables: {
    first: number;
    after?: string;
    where?: RefTestFilterInput;
    order?: any;
  }): Promise<any> {
    return this.queryRef.fetchMore({ variables });
  }

  updateCountQueries(): void {
    const filter = this.filterState.filter();
    const baseFilter = this.queryBuilder.buildWhereFilter(filter, {
      excludeStatus: true,
    });

    this.countsQueryRef.refetch({
      allWhere: baseFilter,
      pendingWhere: this.queryBuilder.mergeFilters(baseFilter, {
        status: { eq: RefTestStatus.Pending },
      }),
      inProgressWhere: this.queryBuilder.mergeFilters(baseFilter, {
        status: { eq: RefTestStatus.InProgress },
      }),
      completedWhere: this.queryBuilder.mergeFilters(baseFilter, {
        status: { eq: RefTestStatus.Completed },
      }),
      expiredWhere: this.queryBuilder.mergeFilters(baseFilter, {
        status: { eq: RefTestStatus.Expired },
      }),
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

    this.deleteRefTestsGQL
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
        takeUntilDestroyed(this.destroyRef),
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

    this.sendInvitationsGQL
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
        takeUntilDestroyed(this.destroyRef),
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

    this.sendResultsGQL
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
        takeUntilDestroyed(this.destroyRef),
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

    this.generateReportGQL
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
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }
}
