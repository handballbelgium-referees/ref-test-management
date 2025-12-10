import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of, shareReplay } from 'rxjs';
import { User } from '../models/user';

@Injectable({
  providedIn: 'root',
})
export class Auth {
  private readonly _http = inject(HttpClient);

  // Fetch user with error handling - returns null if not authenticated
  readonly user = toSignal(
    this._http.get<User>('/Account/User').pipe(
      catchError((error: HttpErrorResponse) => {
        // If 401, 404, 302 (redirect), or 500 (proxy error from redirect), user is not logged in
        // The backend returns 302 redirect when not authenticated, which the proxy converts to 500
        if (
          error.status === 401 ||
          error.status === 404 ||
          error.status === 302 ||
          error.status === 500
        ) {
          return of(null);
        }
        // For other errors, also return null but log for debugging
        console.error('Error fetching user:', error);
        return of(null);
      }),
      shareReplay(1)
    ),
    { initialValue: null }
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
