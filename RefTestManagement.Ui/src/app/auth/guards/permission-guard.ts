import { toObservable } from '@angular/core/rxjs-interop';
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { filter, first, map } from 'rxjs';
import { PermissionsService } from '../services/permissions';

/**
 * Creates a route guard that checks whether the current user has the given permission.
 * Waits for permissions to finish loading before evaluating, then redirects to `/` if
 * the user lacks the required permission.
 * Authentication is assumed to be checked by {@link authGuard} applied before this guard.
 */
export function permissionGuard(permission: string): CanActivateFn {
  return () => {
    const permissionsService = inject(PermissionsService);
    const router = inject(Router);

    return toObservable(permissionsService.permissions).pipe(
      filter((perms) => perms != null),
      first(),
      map(() => {
        if (permissionsService.hasPermission(permission)) {
          return true;
        }
        return router.createUrlTree(['/']);
      }),
    );
  };
}
