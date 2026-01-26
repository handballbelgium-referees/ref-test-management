import { DestroyRef, WritableSignal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Apollo } from 'apollo-angular';
import { catchError, EMPTY, finalize, Observable, tap } from 'rxjs';
import { IsolatedBannerManager } from '../../services/banner';

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
  const { onSuccess = () => {}, onError = () => {}, onComplete = () => {} } = callbacks;

  mutation$
    .pipe(
      tap((result: Apollo.MutateResult<TData>) => {
        // Only call onSuccess when loading is finished
        loadingSignal.set(result.loading ?? false);
        if (!result.loading) {
          const mapped = mapResult ? mapResult(result) : (result as unknown as TResult);
          onSuccess(mapped);
        }
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
  value: T | null | undefined,
  amount: number = 1,
): T | null | undefined {
  if (!value || value.totalCount === undefined) return value;
  return { ...value, totalCount: value.totalCount - amount };
}

export function increment<T extends { totalCount?: number }>(
  value: T | null | undefined,
  amount: number = 1,
): T | null | undefined {
  if (!value || value.totalCount === undefined) return value;
  return { ...value, totalCount: value.totalCount + amount };
}

export type DialogOperationCallback<TParams = void> = (
  ids: string[],
  destroyRef: DestroyRef,
  params: TParams,
  bannerManager?: IsolatedBannerManager,
) => void;

export function createDialogOperation<TParams = void>(
  isOpen: WritableSignal<boolean>,
  getSelectedIds: () => string[],
  callback: DialogOperationCallback<TParams>,
) {
  return {
    initiate(): void {
      if (getSelectedIds().length === 0) return;
      isOpen.set(true);
    },
    confirm(destroyRef: DestroyRef, params: TParams, bannerManager?: IsolatedBannerManager): void {
      // params must be provided if TParams is not void
      const ids = getSelectedIds();
      if (ids.length === 0) return;

      isOpen.set(false);
      callback(ids, destroyRef, params, bannerManager);
    },
    cancel(): void {
      isOpen.set(false);
    },
  };
}
