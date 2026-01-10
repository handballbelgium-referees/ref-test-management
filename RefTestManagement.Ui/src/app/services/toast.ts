import {
  ApplicationRef,
  ComponentRef,
  createComponent,
  EnvironmentInjector,
  inject,
  Injectable,
} from '@angular/core';
import { TOAST_DURATION } from '../constants';
import { Toast as ToastComponent } from '../shared/components/toast/toast';

export type ToastType = 'error' | 'warning' | 'success' | 'info';
export type ToastDuration = number; // milliseconds

export interface ToastOptions {
  type?: ToastType;
  duration?: ToastDuration;
  dismissible?: boolean;
  action?: {
    label: string;
    callback: () => void;
  };
}

/**
 * Toast notification service
 * Dynamically creates and manages a ToastComponent
 */
@Injectable({
  providedIn: 'root',
})
export class Toast {
  private _toastComponentRef: ComponentRef<ToastComponent> | null = null;
  private readonly _appRef = inject(ApplicationRef);
  private readonly _injector = inject(EnvironmentInjector);

  /**
   * Show an error toast
   */
  error(message: string, duration: ToastDuration = TOAST_DURATION.ERROR): void {
    this.show(message, { type: 'error', duration });
  }

  /**
   * Show a warning toast
   */
  warning(message: string, duration: ToastDuration = TOAST_DURATION.WARNING): void {
    this.show(message, { type: 'warning', duration });
  }

  /**
   * Show a success toast
   */
  success(message: string, duration: ToastDuration = TOAST_DURATION.SUCCESS): void {
    this.show(message, { type: 'success', duration });
  }

  /**
   * Show an info toast
   */
  info(message: string, duration: ToastDuration = TOAST_DURATION.INFO): void {
    this.show(message, { type: 'info', duration });
  }

  /**
   * Show a toast notification
   */
  show(message: string, options: ToastOptions = {}): void {
    const { type = 'info', duration = TOAST_DURATION.INFO, dismissible = true, action } = options;

    // Create component if it doesn't exist
    if (!this._toastComponentRef) {
      this.createToastComponent();
    }

    // Add toast to the component
    const toastComponent = this._toastComponentRef!.instance;
    const toastId = toastComponent.add({ message, type, dismissible, action });

    // Auto-dismiss after duration
    if (duration > 0) {
      setTimeout(() => {
        toastComponent.dismiss(toastId);
      }, duration);
    }
  }

  /**
   * Create the toast component and attach it to the DOM
   */
  private createToastComponent(): void {
    this._toastComponentRef = createComponent(ToastComponent, {
      environmentInjector: this._injector,
    });

    // Attach to application
    this._appRef.attachView(this._toastComponentRef.hostView);

    // Append to body
    const domElem = this._toastComponentRef.location.nativeElement as HTMLElement;
    document.body.appendChild(domElem);
  }
}
