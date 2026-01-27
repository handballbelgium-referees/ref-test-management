import { DestroyRef, effect, inject, Injector, Signal, signal } from '@angular/core';
import { Banner, IsolatedBannerManager } from '../../services/banner';

export type DialogOperationCallback<TParams = void> = (
  ids: string[],
  destroyRef: DestroyRef,
  params: TParams,
  bannerManager?: IsolatedBannerManager,
) => { loading: Signal<boolean>; success: Signal<boolean> } | void;

export function createDialogOperation<TParams = void, TExtra = void>(
  callback: DialogOperationCallback<TParams>,
  getSelectedIds?: () => string[],
) {
  const show = signal(false);
  const loading = signal(false);
  const initialData = signal<TExtra | undefined>(undefined);
  const injector = inject(Injector);
  const bannerManager = signal<IsolatedBannerManager>({} as IsolatedBannerManager);

  return {
    initiate(extra?: TExtra): void {
      if (getSelectedIds && getSelectedIds().length === 0) return;
      initialData.set(extra);
      bannerManager.set(injector.get(Banner).createIsolated());
      show.set(true);
    },
    confirm(destroyRef: DestroyRef, params: TParams): void {
      if (getSelectedIds && getSelectedIds().length === 0) return;
      const ids = getSelectedIds ? getSelectedIds() : [];

      let closed = false;
      const dialogControl = {
        close: () => {
          if (!closed) {
            show.set(false);
            initialData.set(undefined);
            closed = true;
          }
        },
      };

      const result = callback(ids, destroyRef, params, bannerManager());
      if (result) {
        const effectRef = effect(
          () => {
            const isLoading = result.loading();
            const isSuccess = result.success();
            loading.set(isLoading);

            // Auto-close when loading completes (assumes success if no error was thrown)
            if (!isLoading && isSuccess) {
              dialogControl.close();
              effectRef.destroy();
            } else if (!isLoading) {
              effectRef.destroy();
            }
          },
          { injector },
        );
      }
    },
    cancel(): void {
      show.set(false);
      initialData.set(undefined);
    },
    loading: loading.asReadonly(),
    show: show.asReadonly(),
    initialData,
    bannerManager: bannerManager.asReadonly(),
  };
}
