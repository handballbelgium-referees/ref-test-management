import { describe, expect, it } from 'vitest';
import { refTestTokenFromState, resolveRefTestToken } from './ref-test-token-state';

describe('RefTest token state', () => {
  it('reads an opaque token from router history state', () => {
    expect(refTestTokenFromState({ refTestToken: 'participant-token' })).toBe(
      'participant-token',
    );
  });

  it('ignores missing, blank, and non-string values', () => {
    expect(refTestTokenFromState(null)).toBeNull();
    expect(refTestTokenFromState({ refTestToken: '  ' })).toBeNull();
    expect(refTestTokenFromState({ refTestToken: 42 })).toBeNull();
  });

  it('prefers an invitation route token, then navigation state, then history state', () => {
    expect(
      resolveRefTestToken(
        'route-token',
        { refTestToken: 'navigation-token' },
        { refTestToken: 'history-token' },
      ),
    ).toBe('route-token');
    expect(
      resolveRefTestToken(
        null,
        { refTestToken: 'navigation-token' },
        { refTestToken: 'history-token' },
      ),
    ).toBe('navigation-token');
    expect(resolveRefTestToken(null, null, { refTestToken: 'history-token' })).toBe(
      'history-token',
    );
  });
});
