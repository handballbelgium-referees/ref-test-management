import { Location } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, Router } from '@angular/router';
import { BehaviorSubject, of, Subject } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  AcceptPrivacyNoticeGQL,
  CreateRefTestSessionGQL,
  GetPrivacyNoticeGQL,
  GetRefTestByTokenGQL,
  WithdrawConsentGQL,
} from '../../../../graphql/generated';
import { REF_TEST_SESSION_TOKEN_STATE_KEY } from '../ref-test-token-state';
import { RefTestWelcome } from './ref-test-welcome';

describe('RefTestWelcome', () => {
  let welcome: RefTestWelcome;
  let withdrawalMutation: ReturnType<typeof vi.fn>;
  let acceptPrivacyNoticeMutation: ReturnType<typeof vi.fn>;
  let createRefTestSessionMutation: ReturnType<typeof vi.fn>;
  let fetchPrivacyNotice: ReturnType<typeof vi.fn>;
  let navigate: ReturnType<typeof vi.fn>;
  let routeParamMap: BehaviorSubject<ParamMap>;
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
    createRefTestSessionMutation = vi.fn().mockReturnValue(
      of({
        data: {
          createRefTestSession: {
            participantSessionDto: { sessionToken: 'rts1.session-token' },
            errors: [],
          },
        },
      }),
    );
    fetchPrivacyNotice = vi.fn().mockReturnValue(
      of({ data: { privacyNotice: { noticeVersion: 'notice-1' } } }),
    );
    navigate = vi.fn();
    routeParamMap = new BehaviorSubject(convertToParamMap({ token: 'participant-token' }));
    refTestByTokenSubject = new Subject();

    TestBed.configureTestingModule({
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: routeParamMap,
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
        { provide: CreateRefTestSessionGQL, useValue: { mutate: createRefTestSessionMutation } },
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

  it('requires privacy consent again when the route changes to another invitation', () => {
    welcome.privacyAccepted.set(true);

    routeParamMap.next(convertToParamMap({ token: 'another-invitation' }));

    expect(welcome.privacyAccepted()).toBe(false);
  });

  it('exchanges the invitation token before starting on the tokenless route', () => {
    welcome.privacyAccepted.set(true);

    welcome.startRefTest();

    expect(fetchPrivacyNotice).toHaveBeenCalledOnce();
    expect(acceptPrivacyNoticeMutation).toHaveBeenCalledWith({
      variables: {
        input: { token: 'participant-token', noticeVersion: 'notice-1' },
      },
      useMutationLoading: false,
    });
    expect(createRefTestSessionMutation).toHaveBeenCalledWith({
      variables: { input: { token: 'participant-token' } },
      useMutationLoading: false,
    });
    expect(navigate).toHaveBeenCalledWith(['/ref-test/take'], {
      replaceUrl: true,
      state: { [REF_TEST_SESSION_TOKEN_STATE_KEY]: 'rts1.session-token' },
    });
  });

  it('exchanges the participant credential before resuming on the tokenless route', () => {
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

    expect(createRefTestSessionMutation).toHaveBeenCalledWith({
      variables: { input: { token: 'participant-token' } },
      useMutationLoading: false,
    });
    expect(navigate).toHaveBeenCalledWith(['/ref-test/take'], {
      replaceUrl: true,
      state: { [REF_TEST_SESSION_TOKEN_STATE_KEY]: 'rts1.session-token' },
    });
  });

  it('shows an error and stays on the welcome route if session creation fails', () => {
    createRefTestSessionMutation.mockReturnValue(
      of({
        data: {
          createRefTestSession: {
            participantSessionDto: null,
            errors: [{ __typename: 'RefTestNotFoundError', message: 'Not found' }],
          },
        },
      }),
    );
    welcome.privacyAccepted.set(true);

    welcome.startRefTest();

    expect(welcome.sessionCreationError()).toBe(true);
    expect(navigate).not.toHaveBeenCalled();
  });
});
