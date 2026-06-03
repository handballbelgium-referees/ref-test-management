import { Service, signal } from '@angular/core';

/**
 * Toast notification durations in milliseconds
 */
export const BANNER_DURATION = {
  ERROR: 5000,
  WARNING: 5000,
  SUCCESS: 6000,
  INFO: 3000,
} as const;

export type BannerType = 'error' | 'warning' | 'success' | 'info';
export type BannerDuration = number;

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
 * Base banner manager used by both global and dialog banners
 */
export abstract class BannerManagerBase {
  protected _nextId = 1;
  readonly banners = signal<IBanner[]>([]);

  error(
    message: string,
    submessage = '',
    duration: BannerDuration = BANNER_DURATION.ERROR,
    action?: { label: string; callback: () => void },
  ): void {
    this.show(message, { type: 'error', duration, submessage, action });
  }

  warning(
    message: string,
    submessage = '',
    duration: BannerDuration = BANNER_DURATION.WARNING,
    action?: { label: string; callback: () => void },
  ): void {
    this.show(message, { type: 'warning', duration, submessage, action });
  }

  success(
    message: string,
    submessage = '',
    duration: BannerDuration = BANNER_DURATION.SUCCESS,
    action?: { label: string; callback: () => void },
  ): void {
    this.show(message, { type: 'success', duration, submessage, action });
  }

  info(
    message: string,
    submessage = '',
    duration: BannerDuration = BANNER_DURATION.INFO,
    action?: { label: string; callback: () => void },
  ): void {
    this.show(message, { type: 'info', duration, submessage, action });
  }

  protected show(message: string, options: BannerOptions = {}): void {
    const {
      type = 'info',
      duration = BANNER_DURATION.INFO,
      dismissible = true,
      action,
      submessage = '',
    } = options;

    // Prevent duplicate banners (same message + type)
    const exists = this.banners().some((b) => b.message === message && b.type === type);

    if (exists) {
      return;
    }

    const id = this._nextId++;
    const banner: IBanner = {
      id,
      message,
      type,
      dismissible,
      action,
      submessage,
    };

    this.banners.update((b) => [...b, banner]);

    if (duration > 0) {
      setTimeout(() => this.dismiss(id), duration);
    }
  }

  dismiss(id: number): void {
    this.banners.update((b) => b.filter((x) => x.id !== id));
  }

  executeAction(id: number, action: () => void): void {
    action();
    this.dismiss(id);
  }

  /**
   * Useful for dialogs on close
   */
  clear(): void {
    this.banners.set([]);
  }
}

@Service()
export class Banner extends BannerManagerBase {
  /**
   * Creates an isolated banner manager for dialogs
   */
  createIsolated(): IsolatedBannerManager {
    return new IsolatedBannerManager();
  }
}

/**
 * Dialog-scoped banner manager
 */
export class IsolatedBannerManager extends BannerManagerBase {
  /**
   * Dialog-friendly defaults
   */
  protected override show(message: string, options: BannerOptions = {}): void {
    super.show(message, {
      duration: options.duration ?? BANNER_DURATION.INFO,
      dismissible: options.dismissible ?? true,
      ...options,
    });
  }
}
