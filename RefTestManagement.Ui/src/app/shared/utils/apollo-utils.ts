import { DestroyRef, Signal, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ObservableQuery } from '@apollo/client';
import { Apollo } from 'apollo-angular';
import { catchError, EMPTY, finalize, Observable, tap } from 'rxjs';

/**
 * Helper for Apollo mutations with standardized callbacks and loading signal
 */
export type MutationCallbacks<T = unknown> = {
  /** Called immediately when the mutation starts */
  onStart?: () => void;

  /** Called once when the mutation finishes successfully */
  onSuccess?: (result: T) => void;

  /** Called if the mutation fails */
  onError?: (error: unknown) => void;

  /** Called when the mutation completes (success or error) */
  onComplete?: () => void;
};

export function runMutation<TData, TResult>(
  mutation$: Observable<Apollo.MutateResult<TData>>,
  destroyRef: DestroyRef,
  callbacks: MutationCallbacks<TResult> = {},
  mapResult?: (result: Apollo.MutateResult<TData>) => TResult,
): {
  loading: Signal<boolean>;
  success: Signal<boolean>;
  error: Signal<unknown>;
  data: Signal<TResult | null>;
} {
  const loading = signal(false);
  const success = signal(false);
  const error = signal<unknown>(null);
  const data = signal<TResult | null>(null);

  const {
    onStart = () => {},
    onSuccess = () => {},
    onError = () => {},
    onComplete = () => {},
  } = callbacks;

  onStart();

  mutation$
    .pipe(
      tap((result) => loading.set(result.loading ?? false)),
      tap((result) => {
        if (result.loading) return;

        const mapped = mapResult ? mapResult(result) : (result as unknown as TResult);
        data.set(mapped);
        success.set(true);
        onSuccess(mapped);
        error.set(null);
      }),
      catchError((err) => {
        onError(err);
        error.set(err);
        success.set(false);
        return EMPTY;
      }),
      finalize(() => {
        loading.set(false);
        onComplete();
      }),
      takeUntilDestroyed(destroyRef),
    )
    .subscribe();

  return {
    loading: loading.asReadonly(),
    success: success.asReadonly(),
    error: error.asReadonly(),
    data: data.asReadonly(),
  };
}

/**
 * Unified helper for Apollo queries (fetch and watchQuery)
 */
export interface QueryCallbacks<TResult> {
  onStart?: () => void;
  onSuccess?: (result: TResult) => void;
  onError?: (error: unknown) => void;
  onComplete?: () => void;
}

export interface QueryState<TResult> {
  loading: Signal<boolean>;
  success: Signal<boolean>;
  error: Signal<unknown>;
  data: Signal<TResult | null>;
}

export function runQuery<TData, TResult>(
  query$: Observable<Apollo.QueryResult<TData> | ObservableQuery.Result<TData>>,
  destroyRef: DestroyRef,
  callbacks: QueryCallbacks<TResult> = {},
  mapResult?: (result: Apollo.QueryResult<TData> | ObservableQuery.Result<TData>) => TResult,
): QueryState<TResult> {
  const loading = signal(false);
  const success = signal(false);
  const error = signal<unknown>(null);
  const data = signal<TResult | null>(null);

  const {
    onStart = () => {},
    onSuccess = () => {},
    onError = () => {},
    onComplete = () => {},
  } = callbacks;

  onStart();

  query$
    .pipe(
      tap((result: Apollo.QueryResult<TData> | ObservableQuery.Result<TData>) => {
        // If the result has a loading property (watchQuery), update loading
        if ('loading' in result) {
          loading.set(result.loading);
        }

        // Only process when actual data is present
        const isLoaded = !('loading' in result) || result.loading === false;
        if (!isLoaded) return;

        const mapped = mapResult ? mapResult(result) : (result.data as unknown as TResult);
        success.set(true);
        data.set(mapped);
        onSuccess(mapped);
        error.set(null);
      }),
      catchError((err: unknown) => {
        onError(err);
        error.set(err);
        success.set(false);
        return EMPTY;
      }),
      finalize(() => {
        onComplete();
        loading.set(false);
      }),
      takeUntilDestroyed(destroyRef),
    )
    .subscribe();

  return {
    loading: loading.asReadonly(),
    success: success.asReadonly(),
    data: data.asReadonly(),
    error: error.asReadonly(),
  };
}

export function decrement<T extends { totalCount?: number }>(
  value?: T | null,
  amount: number = 1,
): T | null | undefined {
  if (!value || value.totalCount === undefined) return value;
  return { ...value, totalCount: value.totalCount - amount };
}

export function increment<T extends { totalCount?: number }>(
  value?: T | null,
  amount: number = 1,
): T | null | undefined {
  if (!value || value.totalCount === undefined) return value;
  return { ...value, totalCount: value.totalCount + amount };
}
