import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { EMPTY, of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  AcceptPrivacyNoticeGQL,
  GetPrivacyNoticeGQL,
  GetRefTestByTokenGQL,
  WithdrawConsentGQL,
} from '../../../../graphql/generated';
import { RefTestWelcome } from './ref-test-welcome';

describe('RefTestWelcome consent withdrawal', () => {
  let welcome: RefTestWelcome;
  let withdrawalMutation: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    withdrawalMutation = vi.fn().mockReturnValue(
      of({
        data: {
          withdrawConsent: {
            boolean: true,
            errors: [],
          },
        },
      }),
    );

    TestBed.configureTestingModule({
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ token: 'participant-token' })),
          },
        },
        { provide: Router, useValue: { navigate: vi.fn() } },
        {
          provide: GetRefTestByTokenGQL,
          useValue: { watch: vi.fn(() => ({ valueChanges: EMPTY })) },
        },
        { provide: GetPrivacyNoticeGQL, useValue: {} },
        { provide: AcceptPrivacyNoticeGQL, useValue: {} },
        { provide: WithdrawConsentGQL, useValue: { mutate: withdrawalMutation } },
      ],
    });

    welcome = TestBed.runInInjectionContext(() => new RefTestWelcome());
  });

  it('shows a queued acknowledgement after the API accepts the request', () => {
    welcome.showWithdrawDialog.set(true);

    welcome.withdrawConsent();

    expect(withdrawalMutation).toHaveBeenCalledWith({
      variables: { input: { token: 'participant-token' } },
    });
    expect(welcome.withdrawalQueued()).toBe(true);
    expect(welcome.showWithdrawDialog()).toBe(false);
  });
});
