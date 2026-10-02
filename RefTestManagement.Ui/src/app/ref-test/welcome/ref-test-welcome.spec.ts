import { Location } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of, Subject } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  AcceptPrivacyNoticeGQL,
  GetPrivacyNoticeGQL,
  GetRefTestByTokenGQL,
  WithdrawConsentGQL,
} from '../../../../graphql/generated';
import { REF_TEST_TOKEN_STATE_KEY } from '../ref-test-token-state';
import { RefTestWelcome } from './ref-test-welcome';

describe('RefTestWelcome', () => {
  let welcome: RefTestWelcome;
  let withdrawalMutation: ReturnType<typeof vi.fn>;
  let acceptPrivacyNoticeMutation: ReturnType<typeof vi.fn>;
  let fetchPrivacyNotice: ReturnType<typeof vi.fn>;
  let navigate: ReturnType<typeof vi.fn>;
  let refTestByTokenSubject: Subject<unknown>;

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
    acceptPrivacyNoticeMutation = vi.fn().mockReturnValue(
      of({
        data: {
          acceptPrivacyNotice: {
            participantRefTest: { __typename: 'ParticipantRefTest' },
          },
        },
      }),
    );
    fetchPrivacyNotice = vi.fn().mockReturnValue(
      of({ data: { privacyNotice: { noticeVersion: 'notice-1' } } }),
    );
    navigate = vi.fn();
    refTestByTokenSubject = new Subject();

    TestBed.configureTestingModule({
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ token: 'participant-token' })),
          },
        },
        { provide: Router, useValue: { navigate, currentNavigation: () => null } },
        { provide: Location, useValue: { getState: () => null } },
        {
          provide: GetRefTestByTokenGQL,
          useValue: {
            watch: vi.fn(() => ({ valueChanges: refTestByTokenSubject })),
          },
        },
        { provide: GetPrivacyNoticeGQL, useValue: { fetch: fetchPrivacyNotice } },
        { provide: AcceptPrivacyNoticeGQL, useValue: { mutate: acceptPrivacyNoticeMutation } },
        { provide: WithdrawConsentGQL, useValue: { mutate: withdrawalMutation } },
      ],
    });

    welcome = TestBed.runInInjectionContext(() => new RefTestWelcome());
    refTestByTokenSubject.next({
      data: {
        refTestByToken: {
          __typename: 'ParticipantRefTest',
          status: 'INVITED',
          currentQuestionIndex: null,
        },
      },
      loading: false,
    });
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

  it('starts a RefTest on a tokenless route while keeping the token in history state', () => {
    welcome.privacyAccepted.set(true);

    welcome.startRefTest();

    expect(fetchPrivacyNotice).toHaveBeenCalledOnce();
    expect(acceptPrivacyNoticeMutation).toHaveBeenCalledWith({
      variables: {
        input: { token: 'participant-token', noticeVersion: 'notice-1' },
      },
      useMutationLoading: false,
    });
    expect(navigate).toHaveBeenCalledWith(['/ref-test/take'], {
      replaceUrl: true,
      state: { [REF_TEST_TOKEN_STATE_KEY]: 'participant-token' },
    });
  });

  it('resumes an existing RefTest on the tokenless route', () => {
    refTestByTokenSubject.next({
      data: {
        refTestByToken: {
          __typename: 'ParticipantRefTest',
          status: 'IN_PROGRESS',
          currentQuestionIndex: 1,
        },
      },
      loading: false,
    });

    expect(navigate).toHaveBeenCalledWith(['/ref-test/take'], {
      replaceUrl: true,
      state: { [REF_TEST_TOKEN_STATE_KEY]: 'participant-token' },
    });
  });
});
