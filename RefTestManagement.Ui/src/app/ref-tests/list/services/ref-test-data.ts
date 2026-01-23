import { computed, DestroyRef, inject, Injectable } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { catchError, finalize, map, of, tap } from 'rxjs';
import {
  DeleteRefTestsGQL,
  GenerateReportGQL,
  GetRefTestsCountGQL,
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
  private readonly getRefTestsCountGQL = inject(GetRefTestsCountGQL);
  private readonly deleteRefTestsGQL = inject(DeleteRefTestsGQL);
  private readonly sendInvitationsGQL = inject(SendRefTestInvitationsGQL);
  private readonly sendResultsGQL = inject(SendRefTestResultsGQL);
  private readonly generateReportGQL = inject(GenerateReportGQL);
  private readonly destroyRef = inject(DestroyRef);
  private readonly filterState = inject(RefTestFilterState);
  private readonly queryBuilder = inject(RefTestQueryBuilder);

  private readonly queryRef = this.getRefTestsGQL.watch({
    fetchPolicy: 'cache-first',
  });

  private readonly countQueries = {
    all: this.getRefTestsCountGQL.watch({ fetchPolicy: 'cache-first' }),
    pending: this.getRefTestsCountGQL.watch({ fetchPolicy: 'cache-first' }),
    inProgress: this.getRefTestsCountGQL.watch({ fetchPolicy: 'cache-first' }),
    completed: this.getRefTestsCountGQL.watch({ fetchPolicy: 'cache-first' }),
    expired: this.getRefTestsCountGQL.watch({ fetchPolicy: 'cache-first' }),
  };

  readonly loading = toSignal(this.queryRef.valueChanges.pipe(map((result) => result.loading)), {
    initialValue: true,
  });

  readonly queryResult = toSignal(this.queryRef.valueChanges);

  // Create signals for each count query
  private readonly allCountResult = toSignal(
    this.countQueries.all.valueChanges.pipe(map((r) => r.data?.refTests?.totalCount ?? 0)),
    { initialValue: 0 },
  );

  private readonly pendingCountResult = toSignal(
    this.countQueries.pending.valueChanges.pipe(map((r) => r.data?.refTests?.totalCount ?? 0)),
    { initialValue: 0 },
  );

  private readonly inProgressCountResult = toSignal(
    this.countQueries.inProgress.valueChanges.pipe(map((r) => r.data?.refTests?.totalCount ?? 0)),
    { initialValue: 0 },
  );

  private readonly completedCountResult = toSignal(
    this.countQueries.completed.valueChanges.pipe(map((r) => r.data?.refTests?.totalCount ?? 0)),
    { initialValue: 0 },
  );

  private readonly expiredCountResult = toSignal(
    this.countQueries.expired.valueChanges.pipe(map((r) => r.data?.refTests?.totalCount ?? 0)),
    { initialValue: 0 },
  );

  readonly statusCounts = computed(() => ({
    all: this.allCountResult(),
    pending: this.pendingCountResult(),
    inProgress: this.inProgressCountResult(),
    completed: this.completedCountResult(),
    expired: this.expiredCountResult(),
  }));

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

    this.countQueries.all.refetch({ where: baseFilter });

    this.countQueries.pending.refetch({
      where: this.queryBuilder.mergeFilters(baseFilter, {
        status: { eq: RefTestStatus.Pending },
      }),
    });

    this.countQueries.inProgress.refetch({
      where: this.queryBuilder.mergeFilters(baseFilter, {
        status: { eq: RefTestStatus.InProgress },
      }),
    });

    this.countQueries.completed.refetch({
      where: this.queryBuilder.mergeFilters(baseFilter, {
        status: { eq: RefTestStatus.Completed },
      }),
    });

    this.countQueries.expired.refetch({
      where: this.queryBuilder.mergeFilters(baseFilter, {
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
