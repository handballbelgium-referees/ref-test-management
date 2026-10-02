import { Location } from '@angular/common';
import { inject } from '@angular/core';
import { CanActivateFn, RedirectCommand, Router } from '@angular/router';
import {
  REF_TEST_LEGACY_TOKEN_STATE_KEY,
  refTestInvitationTokenFromState,
  resolveRefTestSessionToken,
  resolveRefTestToken,
} from '../ref-test-token-state';

const INVITATION_TOKEN_PATTERN = /^[0-9a-f]{32}$/;

function redirectToWelcomeWithInvitationToken(
  token: string,
  queryParams: Record<string, string | string[]>,
  router: Router,
): RedirectCommand {
  return new RedirectCommand(
    router.createUrlTree(['/ref-test/welcome'], { queryParams }),
    {
      replaceUrl: true,
      state: { [REF_TEST_LEGACY_TOKEN_STATE_KEY]: token },
    },
  );
}

export const refTestInvitationFragmentGuard: CanActivateFn = (route) => {
  const router = inject(Router);
  const token = route.fragment;
  if (!token || !INVITATION_TOKEN_PATTERN.test(token)) return router.parseUrl('/');

  return redirectToWelcomeWithInvitationToken(token, route.queryParams, router);
};

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

export const refTestTakeGuard: CanActivateFn = (route) => {
  const router = inject(Router);
  const location = inject(Location);
  const navigationState = router.currentNavigation()?.extras.state;
  const historyState = location.getState();

  if (resolveRefTestSessionToken(navigationState, historyState)) return true;

  const invitationToken =
    refTestInvitationTokenFromState(navigationState) ??
    refTestInvitationTokenFromState(historyState);
  if (!invitationToken) return router.parseUrl('/');

  return new RedirectCommand(
    router.createUrlTree(['/ref-test/welcome'], { queryParams: route.queryParams }),
    {
      replaceUrl: true,
      state: { [REF_TEST_LEGACY_TOKEN_STATE_KEY]: invitationToken },
    },
  );
};
