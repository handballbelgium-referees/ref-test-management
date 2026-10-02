import { describe, expect, it } from 'vitest';
import {
  refTestInvitationTokenFromState,
  refTestSessionTokenFromState,
  resolveRefTestSessionToken,
  resolveRefTestToken,
} from './ref-test-token-state';

describe('RefTest token state', () => {
  it('reads a session credential from the dedicated history state key', () => {
    expect(refTestSessionTokenFromState({ refTestSessionToken: 'rts1.session-token' })).toBe(
      'rts1.session-token',
    );
  });

  it('does not treat an invitation credential under the session key as a session', () => {
    expect(refTestSessionTokenFromState({ refTestSessionToken: 'invitation-token' })).toBeNull();
  });

  it('reads a legacy invitation credential for migration but not as a session credential', () => {
    const legacyState = { refTestToken: 'invitation-token' };

    expect(refTestInvitationTokenFromState(legacyState)).toBe('invitation-token');
    expect(refTestSessionTokenFromState(legacyState)).toBeNull();
  });

  it('accepts a session credential saved under the legacy key', () => {
    expect(refTestSessionTokenFromState({ refTestToken: 'rts1.session-token' })).toBe(
      'rts1.session-token',
    );
  });

  it('ignores missing, blank, and non-string state values', () => {
    expect(refTestSessionTokenFromState(null)).toBeNull();
    expect(refTestInvitationTokenFromState({ refTestToken: '  ' })).toBeNull();
    expect(refTestInvitationTokenFromState({ refTestToken: 42 })).toBeNull();
  });

  it('prefers an invitation route credential, then navigation state, then history state', () => {
    expect(
      resolveRefTestToken(
        'route-token',
        { refTestSessionToken: 'rts1.navigation-session' },
        { refTestToken: 'history-invitation' },
      ),
    ).toBe('route-token');
    expect(
      resolveRefTestToken(
        null,
        { refTestSessionToken: 'rts1.navigation-session' },
        { refTestToken: 'history-invitation' },
      ),
    ).toBe('rts1.navigation-session');
    expect(resolveRefTestToken(null, null, { refTestToken: 'history-invitation' })).toBe(
      'history-invitation',
    );
  });

  it('resolves only session credentials for the take route', () => {
    expect(
      resolveRefTestSessionToken(
        { refTestToken: 'invitation-token' },
        { refTestSessionToken: 'rts1.history-session' },
      ),
    ).toBe('rts1.history-session');
    expect(resolveRefTestSessionToken({ refTestToken: 'invitation-token' }, null)).toBeNull();
  });
});
