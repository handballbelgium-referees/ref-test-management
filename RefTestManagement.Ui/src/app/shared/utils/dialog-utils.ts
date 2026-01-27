import { DestroyRef, effect, inject, Injector, Signal, signal } from '@angular/core';
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
  const initialData = signal<TExtra | undefined>(undefined);
  const injector = inject(Injector);

  return {
    initiate(extra?: TExtra): void {
      if (getSelectedIds && getSelectedIds().length === 0) return;
      initialData.set(extra);
      show.set(true);
    },

    confirm(destroyRef: DestroyRef, params: TParams, bannerManager?: IsolatedBannerManager): void {
      if (getSelectedIds && getSelectedIds().length === 0) return;
      const ids = getSelectedIds ? getSelectedIds() : [];

      const result = callback(ids, destroyRef, params, bannerManager);
      if (result) {
        // Watch the loading signal using effect
        const effectRef = effect(
          () => {
            const isLoading = result();
            loading.set(isLoading);

            if (!isLoading) {
              show.set(false);
              initialData.set(undefined);
              effectRef.destroy();
            }
          },
          { injector },
        );
      } else {
        // If no signal returned, close immediately
        show.set(false);
        initialData.set(undefined);
      }
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
