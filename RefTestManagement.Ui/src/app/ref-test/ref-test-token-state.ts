export const REF_TEST_SESSION_TOKEN_STATE_KEY = 'refTestSessionToken';
export const REF_TEST_LEGACY_TOKEN_STATE_KEY = 'refTestToken';

const SESSION_TOKEN_PREFIX = 'rts1.';

function isStateRecord(state: unknown): state is Record<string, unknown> {
  return state !== null && typeof state === 'object';
}

function tokenFromState(state: unknown, key: string): string | null {
  if (!isStateRecord(state) || !(key in state)) return null;

  const token = state[key];
  return typeof token === 'string' && token.trim().length > 0 ? token : null;
}

export function refTestSessionTokenFromState(state: unknown): string | null {
  const sessionToken = tokenFromState(state, REF_TEST_SESSION_TOKEN_STATE_KEY);
  if (sessionToken?.startsWith(SESSION_TOKEN_PREFIX)) return sessionToken;

  const legacyToken = tokenFromState(state, REF_TEST_LEGACY_TOKEN_STATE_KEY);
  return legacyToken?.startsWith(SESSION_TOKEN_PREFIX) ? legacyToken : null;
}

export function refTestInvitationTokenFromState(state: unknown): string | null {
  const legacyToken = tokenFromState(state, REF_TEST_LEGACY_TOKEN_STATE_KEY);
  return legacyToken && !legacyToken.startsWith(SESSION_TOKEN_PREFIX) ? legacyToken : null;
}

export function resolveRefTestToken(
  routeToken: string | null,
  navigationState: unknown,
  historyState: unknown,
): string | null {
  if (routeToken?.trim()) return routeToken;

  return (
    refTestSessionTokenFromState(navigationState) ??
    refTestInvitationTokenFromState(navigationState) ??
    refTestSessionTokenFromState(historyState) ??
    refTestInvitationTokenFromState(historyState)
  );
}

export function resolveRefTestSessionToken(
  navigationState: unknown,
  historyState: unknown,
): string | null {
  return refTestSessionTokenFromState(navigationState) ?? refTestSessionTokenFromState(historyState);
}
