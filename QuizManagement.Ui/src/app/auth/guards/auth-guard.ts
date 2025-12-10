import { inject } from '@angular/core';
import { Router, type CanActivateFn } from '@angular/router';
import { Auth } from '../services/auth';

export const authGuard: CanActivateFn = (_route, _state) => {
  const authService = inject(Auth);
  const router = inject(Router);

  const user = authService.user();

  if (user) {
    return true;
  }

  router.navigate(['/login'], { skipLocationChange: true });

  return false;
};
