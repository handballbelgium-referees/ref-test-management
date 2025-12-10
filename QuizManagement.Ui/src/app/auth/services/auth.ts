import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of, shareReplay, switchMap } from 'rxjs';
import { User } from '../models/user';

@Injectable({
  providedIn: 'root',
})
export class Auth {
  private readonly _http = inject(HttpClient);

  private readonly isAuthenticatedObs = this._http
    .get<boolean>('/Account/IsAuthenticated')
    .pipe(shareReplay(1));

  readonly isAuthenticated = toSignal(this.isAuthenticatedObs);

  readonly user = toSignal(
    this.isAuthenticatedObs.pipe(
      switchMap((authenticated) => {
        if (authenticated) {
          // Fetch user data when authenticated
          return this._http.get<User>('/Account/User').pipe(catchError(() => of(null)));
        }
        // Return null when not authenticated
        return of(null);
      })
    )
  );

  login(): void {
    // Store full current URL to redirect back after login (including Angular dev server)
    const returnUrl = encodeURIComponent(window.location.href);
    window.location.href = `/Account/Login?returnUrl=${returnUrl}`;
  }

  logout(): void {
    // Pass the Angular app URL as returnUrl so backend redirects back here after logout
    const returnUrl = encodeURIComponent(window.location.origin + '/');
    window.location.href = `/Account/Logout?returnUrl=${returnUrl}`;
  }
}
