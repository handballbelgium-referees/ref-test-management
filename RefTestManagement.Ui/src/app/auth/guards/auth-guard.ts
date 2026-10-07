import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { filter, map, take } from 'rxjs';
import { Auth } from '../services/auth';

export const authGuard: CanActivateFn = (_route, _state) => {
  const authService = inject(Auth);
  const router = inject(Router);

  return authService.authenticationState$.pipe(
    filter((state) => state.status !== 'checking'),
    take(1),
    map((state) => {
      if (state.status === 'authenticated') {
        return true;
      }

      if (state.status === 'unauthenticated') {
        authService.login();
        return false;
      }

      return router.parseUrl('/');
    }),
  );
};
