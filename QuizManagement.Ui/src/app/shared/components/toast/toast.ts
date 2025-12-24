import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import {
  faCircleCheck,
  faCircleExclamation,
  faCircleInfo,
  faCircleXmark,
  faXmark,
  IconDefinition,
} from '@fortawesome/free-solid-svg-icons';

export interface IToast {
  id: number;
  message: string;
  type: 'error' | 'warning' | 'success' | 'info';
  dismissible: boolean;
  action?: {
    label: string;
    callback: () => void;
  };
}

/**
 * Toast notification
 * Displays stacked toast messages in the top-right corner
 */
@Component({
  selector: 'app-toast',
  imports: [FaIconComponent],
  templateUrl: './toast.html',
  styleUrls: ['./toast.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Toast {
  readonly toasts = signal<IToast[]>([]);

  // Font Awesome icons
  readonly faXmark = faXmark;

  /**
   * Execute action and dismiss toast
   */
  executeAction(id: number, callback: () => void): void {
    callback();
    this.dismiss(id);
  }

  /**
   * Add a new toast notification
   */
  add(toast: Omit<IToast, 'id'>): number {
    const id = Date.now();
    this.toasts.update((toasts) => [...toasts, { ...toast, id }]);
    return id;
  }

  /**
   * Remove a toast by ID
   */
  dismiss(id: number): void {
    this.toasts.update((toasts) => toasts.filter((t) => t.id !== id));
  }

  /**
   * Get Font Awesome icon for toast type
   */
  getIcon(type: IToast['type']): IconDefinition {
    const icons = {
      error: faCircleXmark,
      warning: faCircleExclamation,
      success: faCircleCheck,
      info: faCircleInfo,
    };
    return icons[type];
  }

  /**
   * Get Tailwind CSS classes for toast type
   */
  getToastClasses(type: IToast['type']): string {
    const classes = {
      error: 'toast--error',
      warning: 'toast--warning',
      success: 'toast--success',
      info: 'toast--info',
    };
    return classes[type];
  }
}
