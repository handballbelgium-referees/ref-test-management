import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, merge, of, Subject, switchMap } from 'rxjs';
import { Permissions } from '../models/permissions';
import { Auth } from './auth';

@Injectable({
  providedIn: 'root',
})
export class PermissionsService {
  private readonly _http = inject(HttpClient);
  private readonly _auth = inject(Auth);

  private readonly _refresh$ = new Subject<void>();

  private readonly _fetch$ = this._http
    .get<string[]>('/Account/Permissions')
    .pipe(catchError(() => of([] as string[])));

  readonly permissions = toSignal(
    merge(
      // Initial load: triggered when auth state is known
      this._auth.isAuthenticated$.pipe(
        switchMap((authenticated) => (authenticated ? this._fetch$ : of([] as string[]))),
      ),
      // Manual refresh: re-fetches permissions without re-checking auth state
      this._refresh$.pipe(switchMap(() => this._fetch$)),
    ),
  );

  /**
   * Forces a re-fetch of permissions from the server.
   * Call this after a token refresh or role change.
   */
  refresh(): void {
    this._refresh$.next();
  }

  /**
   * Returns true if the current user has the given permission.
   * Returns false while permissions are still loading (undefined).
   * Superadmin bypasses all checks. Namespace wildcards (e.g. ref-tests:*) are respected.
   */
  hasPermission(permission: string): boolean {
    const perms = this.permissions();
    if (!perms) return false;
    if (perms.includes(Permissions.Superadmin)) return true;
    if (perms.includes(permission)) return true;

    const colonIdx = permission.indexOf(':');
    if (colonIdx > 0) {
      const ns = permission.slice(0, colonIdx);
      if (perms.includes(`${ns}:*`)) return true;
    }

    return false;
  }
}
