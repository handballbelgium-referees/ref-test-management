import { Location } from '@angular/common';
import { inject } from '@angular/core';
import { CanActivateFn, RedirectCommand, Router } from '@angular/router';
import { REF_TEST_TOKEN_STATE_KEY, resolveRefTestToken } from '../ref-test-token-state';

export const refTestSessionGuard: CanActivateFn = () => {
  const router = inject(Router);
  const location = inject(Location);
  const token = resolveRefTestToken(
    null,
    router.currentNavigation()?.extras.state,
    location.getState(),
  );

  return token ? true : router.parseUrl('/');
};

export const refTestTokenUrlGuard: CanActivateFn = (route) => {
  const token = route.paramMap.get('token');
  if (!token) return inject(Router).parseUrl('/');

  const router = inject(Router);
  return new RedirectCommand(
    router.createUrlTree(['/ref-test/take'], { queryParams: route.queryParams }),
    {
      replaceUrl: true,
      state: { [REF_TEST_TOKEN_STATE_KEY]: token },
    },
  );
};
