import { DestroyRef, Signal, signal } from '@angular/core';
import { IsolatedBannerManager } from '../../services/banner';

export type DialogOperationCallback<TParams = void> = (
  ids: string[],
  destroyRef: DestroyRef,
  params: TParams,
  bannerManager?: IsolatedBannerManager,
) => Signal<boolean> | void;

export function createDialogOperation<TParams = void, TExtra = void>(
  callback: DialogOperationCallback<TParams>,
  getSelectedIds?: () => string[],
) {
  const show = signal(false);
  const loading = signal(false);

  // Store extra data temporarily
  const initialData = signal<TExtra | undefined>(undefined);

  return {
    initiate(extra?: TExtra): void {
      if (getSelectedIds && getSelectedIds().length === 0) return;

      initialData.set(extra);
      show.set(true);
    },
    confirm(destroyRef: DestroyRef, params: TParams, bannerManager?: IsolatedBannerManager): void {
      const ids = getSelectedIds ? getSelectedIds() : [];
      if (ids.length === 0) return;

      show.set(false);

      const result = callback(ids, destroyRef, params, bannerManager);
      if (result) {
        loading.set(result());
      }

      initialData.set(undefined);
    },
    cancel(): void {
      show.set(false);
      initialData.set(undefined);
    },
    loading: loading.asReadonly(),
    show: show.asReadonly(),
    initialData,
  };
}
