import { inject } from '@angular/core';
import { type CanActivateFn } from '@angular/router';
import { map } from 'rxjs';
import { Auth } from '../services/auth';

export const authGuard: CanActivateFn = (_route, _state) => {
  const authService = inject(Auth);

  // Access the underlying observable instead of the signal to avoid loops
  return authService.isAuthenticated$.pipe(
    map((authenticated) => {
      if (authenticated) {
        return true;
      }

      // Only redirect if not authenticated
      authService.login();
      return false;
    })
  );
};
