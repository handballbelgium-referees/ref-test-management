export const REF_TEST_TOKEN_STATE_KEY = 'refTestToken';

// Router history state keeps active sessions reloadable without putting the bearer token in the URL.
export function refTestTokenFromState(state: unknown): string | null {
  if (state === null || typeof state !== 'object' || !(REF_TEST_TOKEN_STATE_KEY in state)) {
    return null;
  }

  const token = state[REF_TEST_TOKEN_STATE_KEY];
  return typeof token === 'string' && token.trim().length > 0 ? token : null;
}

export function resolveRefTestToken(
  routeToken: string | null,
  navigationState: unknown,
  historyState: unknown,
): string | null {
  if (routeToken?.trim()) return routeToken;
  return refTestTokenFromState(navigationState) ?? refTestTokenFromState(historyState);
}
