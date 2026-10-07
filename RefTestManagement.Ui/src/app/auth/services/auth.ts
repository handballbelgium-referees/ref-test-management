import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, filter, map, of, shareReplay, startWith, Subject, switchMap } from 'rxjs';
import { User } from '../models/user';

type AuthenticationState =
  | { status: 'checking' }
  | { status: 'authenticated' }
  | { status: 'unauthenticated' }
  | { status: 'error' };

@Service()
export class Auth {
  private readonly _http = inject(HttpClient);
  private readonly _retryAuthenticationCheck$ = new Subject<void>();

  readonly authenticationState$ = this._retryAuthenticationCheck$.pipe(
    startWith(undefined),
    switchMap(() =>
      this._http.get<boolean>('/Account/IsAuthenticated').pipe(
        map(
          (authenticated): AuthenticationState => ({
            status: authenticated ? 'authenticated' : 'unauthenticated',
          }),
        ),
        catchError(() => of<AuthenticationState>({ status: 'error' })),
        startWith<AuthenticationState>({ status: 'checking' }),
      ),
    ),
    shareReplay(1),
  );

  readonly authenticationState = toSignal(this.authenticationState$, {
    initialValue: { status: 'checking' } as AuthenticationState,
  });

  readonly isAuthenticated$ = this.authenticationState$.pipe(
    filter(
      (state) => state.status === 'authenticated' || state.status === 'unauthenticated',
    ),
    map((state) => state.status === 'authenticated'),
  );

  readonly isAuthenticated = toSignal(this.isAuthenticated$);

  readonly user = toSignal(
    this.isAuthenticated$.pipe(
      switchMap((authenticated) => {
        if (authenticated) {
          // Fetch user data when authenticated
          return this._http.get<User>('/Account/User').pipe(catchError(() => of(null)));
        }
        // Return null when not authenticated
        return of(null);
      }),
    ),
  );

  /**
   * Rechecks authentication after an authentication check fails.
   */
  retryAuthenticationCheck(): void {
    this._retryAuthenticationCheck$.next();
  }

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
