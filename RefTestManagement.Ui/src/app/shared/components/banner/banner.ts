import { Component, inject, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import {
  Banner as BannerService,
  BannerType,
  IsolatedBannerManager,
} from '../../../services/banner';

/**
 * Displays banner messages in the page flow
 * Reads banner state from the global BannerService or an isolated manager
 */
@Component({
  selector: 'app-banner',
  imports: [TranslatePipe],
  templateUrl: './banner.html',
  host: { class: 'block' },
  styles: `
    /* Banner animations */
    @keyframes slide-down {
      from {
        transform: translateY(-100%);
        opacity: 0;
      }
      to {
        transform: translateY(0);
        opacity: 1;
      }
    }

    .animate-slide-down {
      animation: slide-down 0.3s ease-out;
    }
  `,
})
export class Banner {
  private readonly _bannerService = inject(BannerService);

  // Optional isolated banner manager for dialogs
  readonly bannerManager = input<IsolatedBannerManager | null>(null);

  // Optional padding classes for the container
  readonly containerClass = input<string>('');

  // Read banners from either the isolated manager or the global service
  protected readonly banners = () => {
    const manager = this.bannerManager();
    return manager ? manager.banners() : this._bannerService.banners();
  };

  /**
   * Execute action and dismiss banner
   */
  protected executeAction(id: number, action: () => void): void {
    const manager = this.bannerManager();
    if (manager) {
      manager.executeAction(id, action);
    } else {
      this._bannerService.executeAction(id, action);
    }
  }

  /**
   * Remove a banner by ID
   */
  protected dismiss(id: number): void {
    const manager = this.bannerManager();
    if (manager) {
      manager.dismiss(id);
    } else {
      this._bannerService.dismiss(id);
    }
  }

  /**
   * Get Font Awesome icon for banner type
   */
  protected getIcon(type: BannerType): string {
    switch (type) {
      case 'success':
        return 'M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z';
      case 'error':
        return 'M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z';
      case 'warning':
        return 'M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z';
      case 'info':
        return 'M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z';
    }
  }

  /**
   * Get Tailwind CSS classes for banner type
   */
  protected getColorClasses(type: BannerType): string {
    switch (type) {
      case 'success':
        return 'bg-success-50 border-success-200 text-success-800';
      case 'error':
        return 'bg-red-50 border-red-200 text-red-800';
      case 'warning':
        return 'bg-yellow-50 border-yellow-200 text-yellow-800';
      case 'info':
        return 'bg-blue-50 border-blue-200 text-blue-800';
    }
  }

  /**
   * Get icon color classes for banner type
   */
  protected getIconColorClass(type: BannerType): string {
    switch (type) {
      case 'success':
        return 'text-success-600';
      case 'error':
        return 'text-red-600';
      case 'warning':
        return 'text-yellow-600';
      case 'info':
        return 'text-blue-600';
    }
  }
}
