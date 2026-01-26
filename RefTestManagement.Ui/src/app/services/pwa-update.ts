import { DestroyRef, Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SwUpdate } from '@angular/service-worker';
import { TranslateService } from '@ngx-translate/core';
import { interval, switchMap } from 'rxjs';
import { Banner } from './banner';

/**
 * Service to handle PWA updates
 * Checks for updates and notifies the user
 */
@Injectable({
  providedIn: 'root',
})
export class PwaUpdate {
  private readonly _swUpdate = inject(SwUpdate);
  private readonly _bannerService = inject(Banner);
  private readonly _translateService = inject(TranslateService);
  private _updateNotificationShown = false;

  /**
   * Setup PWA update checking with periodic checks and version update notifications
   * @param destroyRef DestroyRef from the caller for proper subscription cleanup
   */
  initializeUpdateCheck(destroyRef: DestroyRef): void {
    if (!this._swUpdate.isEnabled) {
      return;
    }

    // Check for updates on initialization
    this._swUpdate.checkForUpdate();

    // Check for updates every hour (3600000 ms)
    interval(3600000)
      .pipe(
        takeUntilDestroyed(destroyRef),
        switchMap(() => this._swUpdate.checkForUpdate()),
      )
      .subscribe();

    // Listen for available updates
    this._swUpdate.versionUpdates.pipe(takeUntilDestroyed(destroyRef)).subscribe((event) => {
      if (event.type === 'VERSION_READY' && !this._updateNotificationShown) {
        this._updateNotificationShown = true;

        // Show update notification with action button
        this._bannerService.info(
          this._translateService.instant('pwa.update_available'),
          0,
          undefined,
          {
            label: this._translateService.instant('pwa.update_now'),
            callback: () => this.activateUpdate(),
          },
        );
      }
    });
  }

  /**
   * Activate the pending update and reload the app
   */
  activateUpdate(): void {
    this._swUpdate.activateUpdate().then(() => {
      window.location.reload();
    });
  }
}
