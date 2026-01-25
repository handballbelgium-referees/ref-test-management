import { Injectable, signal } from '@angular/core';
import { TOAST_DURATION } from '../constants';

export type BannerType = 'error' | 'warning' | 'success' | 'info';
export type BannerDuration = number; // milliseconds

export interface IBanner {
  id: number;
  message: string;
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
  action?: {
    label: string;
    callback: () => void;
  };
}

/**
 * Banner notification service
 * Manages banner state using signals that Banner components subscribe to
 * Prevents duplicate messages from being shown simultaneously
 */
@Injectable({
  providedIn: 'root',
})
export class Banner {
  private _nextId = 1;

  // Global signal that all Banner components will read from
  readonly banners = signal<IBanner[]>([]);

  /**
   * Show an error banner
   */
  error(message: string, duration: BannerDuration = TOAST_DURATION.ERROR): void {
    this.show(message, { type: 'error', duration });
  }

  /**
   * Show a warning banner
   */
  warning(message: string, duration: BannerDuration = TOAST_DURATION.WARNING): void {
    this.show(message, { type: 'warning', duration });
  }

  /**
   * Show a success banner
   */
  success(message: string, duration: BannerDuration = TOAST_DURATION.SUCCESS): void {
    this.show(message, { type: 'success', duration });
  }

  /**
   * Show an info banner
   */
  info(message: string, duration: BannerDuration = TOAST_DURATION.INFO): void {
    this.show(message, { type: 'info', duration });
  }

  /**
   * Show a banner notification
   * Prevents duplicate messages of the same type from being shown simultaneously
   */
  show(message: string, options: BannerOptions = {}): void {
    const { type = 'info', duration = TOAST_DURATION.INFO, dismissible = true, action } = options;

    // Check if the same message with the same type already exists
    const existingBanner = this.banners().find(
      (b) => b.message === message && b.type === type
    );

    // If duplicate found, don't add a new banner
    if (existingBanner) {
      return;
    }

    const id = this._nextId++;
    const banner: IBanner = { id, message, type, dismissible, action };

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


