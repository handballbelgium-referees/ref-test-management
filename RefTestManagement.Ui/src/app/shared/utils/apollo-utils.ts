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
  callbacks: MutationCallbacks<TResult>,
  destroyRef: DestroyRef,
  mapResult?: (result: Apollo.MutateResult<TData>) => TResult,
): Signal<boolean> {
  const loading = signal(false);

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

        onSuccess(mapped);
      }),
      catchError((error) => {
        onError(error);
        return EMPTY;
      }),
      finalize(() => {
        loading.set(false);
        onComplete();
      }),
      takeUntilDestroyed(destroyRef),
    )
    .subscribe();

  return loading.asReadonly();
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
  data: Signal<TResult | null>;
  error: Signal<unknown>;
}

export function runQueryWithState<TData, TResult = TData>(
  query$: Observable<Apollo.QueryResult<TData> | ObservableQuery.Result<TData>>,
  destroyRef: DestroyRef,
  mapResult?: (result: Apollo.QueryResult<TData> | ObservableQuery.Result<TData>) => TResult,
): QueryState<TResult> {
  const loading = signal(true); // always start as loading
  const data = signal<TResult | null>(null);
  const error = signal<unknown>(null);

  query$
    .pipe(
      tap((result: any) => {
        // If the result has a loading property (watchQuery), update loading
        if ('loading' in result) {
          loading.set(result.loading);
        }

        // Only process when actual data is present
        const isLoaded = !('loading' in result) || result.loading === false;
        if (!isLoaded) return;

        const mapped = mapResult ? mapResult(result) : (result.data as unknown as TResult);

        data.set(mapped);
        error.set(null); // clear previous errors
      }),
      catchError((err: unknown) => {
        error.set(err);
        return EMPTY;
      }),
      finalize(() => {
        loading.set(false); // ensure loading is false on completion
      }),
      takeUntilDestroyed(destroyRef),
    )
    .subscribe();

  return {
    loading: loading.asReadonly(),
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
