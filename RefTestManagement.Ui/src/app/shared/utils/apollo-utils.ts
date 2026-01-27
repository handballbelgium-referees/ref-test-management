import { DestroyRef, WritableSignal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Apollo } from 'apollo-angular';
import { catchError, EMPTY, finalize, Observable, tap } from 'rxjs';

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
  loadingSignal: WritableSignal<boolean>,
  mapResult?: (result: Apollo.MutateResult<TData>) => TResult,
): void {
  const {
    onStart = () => {},
    onSuccess = () => {},
    onError = () => {},
    onComplete = () => {},
  } = callbacks;

  onStart();
  loadingSignal.set(true);

  mutation$
    .pipe(
      tap((result) => {
        // Ignore the initial loading emission
        if (result.loading) return;

        const mapped = mapResult ? mapResult(result) : (result as unknown as TResult);

        onSuccess(mapped);
      }),
      catchError((error) => {
        onError(error);
        return EMPTY;
      }),
      finalize(() => {
        loadingSignal.set(false);
        onComplete();
      }),
      takeUntilDestroyed(destroyRef),
    )
    .subscribe();
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
