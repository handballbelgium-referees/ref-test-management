import { effect, inject, Injector, Signal, signal } from '@angular/core';
import { Banner, IsolatedBannerManager } from '../../services/banner';

export type DialogOperationCallback<TParams = void> = (
  ids: string[],
  params: TParams,
  bannerManager?: IsolatedBannerManager,
) => { loading: Signal<boolean>; success: Signal<boolean>; error?: Signal<unknown> } | void;

export function createDialogOperation<TParams = void, TExtra = void>(
  callback: DialogOperationCallback<TParams>,
  getSelectedIds?: () => string[],
) {
  const show = signal(false);
  const loading = signal(false);
  let confirmationInFlight = false;
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
    confirm(params: TParams): void {
      if (confirmationInFlight || (getSelectedIds && getSelectedIds().length === 0)) return;
      const ids = getSelectedIds ? getSelectedIds() : [];
      confirmationInFlight = true;

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

      let result: ReturnType<DialogOperationCallback<TParams>>;
      try {
        result = callback(ids, params, bannerManager());
      } catch (error) {
        confirmationInFlight = false;
        throw error;
      }

      if (!result) {
        confirmationInFlight = false;
        return;
      }

      let observedLoading = false;
      const effectRef = effect(
        () => {
          const isLoading = result.loading();
          const isSuccess = result.success();
          const hasError = result.error?.() != null;
          if (isLoading) observedLoading = true;

          if (!isLoading && (observedLoading || isSuccess || hasError)) {
            confirmationInFlight = false;
            loading.set(false);
            if (isSuccess) {
              dialogControl.close();
            }
            effectRef.destroy();
          } else {
            loading.set(true);
          }
        },
        { injector },
      );
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
