import { inject } from '@angular/core';
import { type CanActivateFn } from '@angular/router';
import { Auth } from '../services/auth';

export const authGuard: CanActivateFn = (_route, _state) => {
  const authService = inject(Auth);
  const user = authService.user();

  // If user is authenticated, allow access
  if (user) {
    return true;
  }

  // If not authenticated, redirect to login page
  authService.login();
  return false;
};
