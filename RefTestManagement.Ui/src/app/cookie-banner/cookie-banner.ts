import { Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

export const COOKIE_NOTICE_STORAGE_KEY = 'cookie-notice-acknowledged';

// Notice only: the app sets no optional cookies, so this banner informs users and does not gate them.
@Component({
  selector: 'app-cookie-banner',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './cookie-banner.html',
})
export class CookieBanner {
  protected readonly visible = signal(!this.isAcknowledged());

  protected accept(): void {
    try {
      localStorage.setItem(COOKIE_NOTICE_STORAGE_KEY, 'true');
    } catch {
      // Storage can be unavailable (e.g. privacy modes); the banner still closes for this visit.
    }
    this.visible.set(false);
  }

  private isAcknowledged(): boolean {
    try {
      return localStorage.getItem(COOKIE_NOTICE_STORAGE_KEY) === 'true';
    } catch {
      return false;
    }
  }
}
