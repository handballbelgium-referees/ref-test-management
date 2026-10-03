import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

export const privacyConfirmationGuard: CanActivateFn = (route) => {
  const router = inject(Router);
  if (route.fragment?.trim()) return true;

  return router.createUrlTree(['/privacy'], {
    queryParams: route.queryParams,
  });
};
