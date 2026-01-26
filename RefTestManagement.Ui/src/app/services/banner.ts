import { Injectable, signal } from '@angular/core';
import { TOAST_DURATION } from '../constants';

export type BannerType = 'error' | 'warning' | 'success' | 'info';
export type BannerDuration = number; // milliseconds

export interface IBanner {
  id: number;
  message: string;
  submessage: string;
  type: BannerType;
  dismissible: boolean;
  action?: {
    label: string;
    callback: () => void;
  };
}

export interface BannerOptions {
  type?: BannerType;
  duration?: BannerDuration;
  dismissible?: boolean;
  submessage?: string;
  action?: {
    label: string;
    callback: () => void;
  };
}

/**
 * Banner notification service
 * Manages banner state using signals that Banner components subscribe to
 * Prevents duplicate messages from being shown simultaneously
 * Supports both global (page-level) and isolated (dialog-level) instances
 */
@Injectable({
  providedIn: 'root',
})
export class Banner {
  private _nextId = 1;

  // Global signal that page-level Banner components will read from
  readonly banners = signal<IBanner[]>([]);

  /**
   * Create an isolated banner manager for dialogs
   * This creates a separate signal that won't affect the global banners
   */
  createIsolated(): IsolatedBannerManager {
    return new IsolatedBannerManager();
  }

  /**
   * Show an error banner
   */
  error(
    message: string,
    submessage = '',
    duration: BannerDuration = TOAST_DURATION.ERROR,
    action?: { label: string; callback: () => void },
  ): void {
    this.show(message, { type: 'error', duration, submessage, action });
  }

  /**
   * Show a warning banner
   */
  warning(
    message: string,
    submessage = '',
    duration: BannerDuration = TOAST_DURATION.WARNING,
    action?: { label: string; callback: () => void },
  ): void {
    this.show(message, { type: 'warning', duration, submessage, action });
  }

  /**
   * Show a success banner
   */
  success(
    message: string,
    submessage = '',
    duration: BannerDuration = TOAST_DURATION.SUCCESS,
    action?: { label: string; callback: () => void },
  ): void {
    this.show(message, { type: 'success', duration, submessage, action });
  }

  /**
   * Show an info banner
   */
  info(
    message: string,
    duration: BannerDuration = TOAST_DURATION.INFO,
    submessage = '',
    action?: { label: string; callback: () => void },
  ): void {
    this.show(message, { type: 'info', duration, submessage, action });
  }

  /**
   * Show a banner notification
   * Prevents duplicate messages of the same type from being shown simultaneously
   */
  private show(message: string, options: BannerOptions = {}): void {
    const {
      type = 'info',
      duration = TOAST_DURATION.INFO,
      dismissible = true,
      action,
      submessage = '',
    } = options;

    // Check if the same message with the same type already exists
    const existingBanner = this.banners().find((b) => b.message === message && b.type === type);

    // If duplicate found, don't add a new banner
    if (existingBanner) {
      return;
    }

    const id = this._nextId++;
    const banner: IBanner = { id, message, type, dismissible, action, submessage };

    // Add banner to the global state
    this.banners.update((banners) => [...banners, banner]);

    // Auto-dismiss after duration
    if (duration > 0) {
      setTimeout(() => {
        this.dismiss(id);
      }, duration);
    }
  }

  /**
   * Dismiss a banner by ID
   */
  dismiss(id: number): void {
    this.banners.update((banners) => banners.filter((b) => b.id !== id));
  }

  /**
   * Execute action and dismiss banner
   */
  executeAction(id: number, action: () => void): void {
    action();
    this.dismiss(id);
  }
}

/**
 * Isolated banner manager for dialogs
 * Has its own signal that doesn't affect the global banners
 */
export class IsolatedBannerManager {
  private _nextId = 1;
  readonly banners = signal<IBanner[]>([]);

  error(
    message: string,
    submessage = '',
    duration: BannerDuration = TOAST_DURATION.ERROR,
    action?: { label: string; callback: () => void },
  ): void {
    this.show(message, { type: 'error', duration, submessage, action });
  }

  warning(
    message: string,
    submessage = '',
    duration: BannerDuration = TOAST_DURATION.WARNING,
    action?: { label: string; callback: () => void },
  ): void {
    this.show(message, { type: 'warning', duration, submessage, action });
  }

  success(
    message: string,
    submessage = '',
    duration: BannerDuration = TOAST_DURATION.SUCCESS,
    action?: { label: string; callback: () => void },
  ): void {
    this.show(message, { type: 'success', duration, submessage, action });
  }

  info(
    message: string,
    submessage = '',
    duration: BannerDuration = TOAST_DURATION.INFO,
    action?: { label: string; callback: () => void },
  ): void {
    this.show(message, { type: 'info', duration, submessage, action });
  }

  private show(message: string, options: BannerOptions = {}): void {
    const {
      type = 'info',
      duration = TOAST_DURATION.INFO,
      dismissible = true,
      action,
      submessage = '',
    } = options;
    // Check for duplicates
    const existingBanner = this.banners().find((b) => b.message === message && b.type === type);

    if (existingBanner) {
      return;
    }

    const id = this._nextId++;
    const banner: IBanner = { id, message, type, dismissible, action, submessage };

    this.banners.update((banners) => [...banners, banner]);

    if (duration > 0) {
      setTimeout(() => {
        this.dismiss(id);
      }, duration);
    }
  }

  dismiss(id: number): void {
    this.banners.update((banners) => banners.filter((b) => b.id !== id));
  }

  executeAction(id: number, action: () => void): void {
    action();
    this.dismiss(id);
  }
}
