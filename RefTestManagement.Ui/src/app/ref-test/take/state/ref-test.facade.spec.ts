import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  CompleteRefTestGQL,
  GetRefTestByTokenGQL,
  RefTestSessionLockGQL,
  RefTestTimeExtendedGQL,
  SaveRefTestProgressGQL,
  StartRefTestGQL,
  WithdrawConsentGQL,
} from '../../../../../graphql/generated';
import { RefTestFacade } from './ref-test.facade';
import { RefTestStore } from './ref-test.store';

describe('RefTestFacade', () => {
  const sessionToken = 'rts1.session-token';
  let facade: RefTestFacade;
  let store: RefTestStore;
  let withdrawalMutation: ReturnType<typeof vi.fn>;
  let completeMutation: ReturnType<typeof vi.fn>;
  let saveProgressMutation: ReturnType<typeof vi.fn>;
  let getRefTestByTokenFetch: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    completeMutation = vi.fn().mockReturnValue(
      of({
        data: {
          completeRefTest: {
            participantRefTest: null,
          },
        },
      }),
    );
    saveProgressMutation = vi.fn().mockReturnValue(
      of({
        data: {
          saveRefTestProgress: {
            participantRefTest: {
              id: 'ref-test',
              currentQuestionIndex: 0,
              selectedAnswerIds: ['answer-1'],
            },
          },
        },
      }),
    );
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
    getRefTestByTokenFetch = vi.fn().mockReturnValue(
      of({
        data: {
          refTestByToken: {
            __typename: 'ParticipantRefTest',
            status: 'IN_PROGRESS',
          },
        },
      }),
    );

    TestBed.configureTestingModule({
      providers: [
        RefTestStore,
        RefTestFacade,
        { provide: TranslateService, useValue: { currentLang: () => 'en' } },
        { provide: StartRefTestGQL, useValue: {} },
        { provide: CompleteRefTestGQL, useValue: { mutate: completeMutation } },
        { provide: GetRefTestByTokenGQL, useValue: { fetch: getRefTestByTokenFetch } },
        { provide: SaveRefTestProgressGQL, useValue: { mutate: saveProgressMutation } },
        { provide: RefTestTimeExtendedGQL, useValue: {} },
        { provide: RefTestSessionLockGQL, useValue: {} },
        { provide: WithdrawConsentGQL, useValue: { mutate: withdrawalMutation } },
      ],
    });

    facade = TestBed.inject(RefTestFacade);
    store = TestBed.inject(RefTestStore);
  });

  it('shows the queued state only after the API accepts the durable request', () => {
    store.token.set(sessionToken);
    store.showWithdrawDialog.set(true);

    facade.withdrawConsent();

    expect(withdrawalMutation).toHaveBeenCalledWith({
      variables: { input: { token: sessionToken } },
    });
    expect(store.withdrawalQueued()).toBe(true);
    expect(store.showWithdrawDialog()).toBe(false);
  });

  it('keeps the request in the confirmation state when the API does not accept it', () => {
    withdrawalMutation.mockReturnValue(
      of({
        data: {
          withdrawConsent: {
            boolean: false,
            errors: [],
          },
        },
      }),
    );
    store.token.set(sessionToken);
    store.showWithdrawDialog.set(true);

    facade.withdrawConsent();

    expect(store.withdrawError()).toBe(true);
    expect(store.withdrawalQueued()).toBe(false);
    expect(store.showWithdrawDialog()).toBe(true);
  });

  it('retries a failed submission with its original answer snapshot and prevents duplicates', () => {
    const firstResponse = new Subject<unknown>();
    completeMutation.mockReturnValue(firstResponse);
    store.token.set(sessionToken);
    store.currentLanguage.set('fr');
    store.initQuestions([
      {
        id: 'question-1',
        phrase: { en: 'Question' },
        answers: [{ id: 'answer-1', phrase: { en: 'Answer' } }],
      },
    ]);
    store.selectAnswer('question-1', 'answer-1');

    facade.submit();
    const originalInput = completeMutation.mock.calls[0][0].variables.input;
    facade.submit();

    expect(completeMutation).toHaveBeenCalledOnce();

    firstResponse.next({
      data: { completeRefTest: { participantRefTest: null } },
    });
    firstResponse.complete();

    expect(store.error()).toBe('submit_failed');
    expect(store.selectedAnswers()['question-1']).toEqual(new Set(['answer-1']));

    completeMutation.mockReturnValue(
      of({ data: { completeRefTest: { participantRefTest: null } } }),
    );
    facade.submit();

    expect(completeMutation).toHaveBeenCalledTimes(2);
    expect(completeMutation.mock.calls[1][0].variables.input).toBe(originalInput);
    expect(originalInput).toEqual({
      token: sessionToken,
      selectedAnswerIds: ['answer-1'],
      language: 'fr',
    });
  });

  it('restores a committed submission after its response is lost', () => {
    const firstResponse = new Subject<unknown>();
    completeMutation.mockReturnValue(firstResponse);
    getRefTestByTokenFetch.mockReturnValue(
      of({
        data: {
          refTestByToken: {
            __typename: 'ParticipantRefTest',
            status: 'COMPLETED',
            questions: [
              {
                id: 'question-1',
                number: '1',
                phrase: { en: 'Question' },
                answers: [
                  {
                    id: 'answer-1',
                    number: '1',
                    phrase: { en: 'Answer' },
                    isCorrect: true,
                  },
                ],
              },
            ],
            selectedAnswerIds: ['answer-1'],
            questionScore: 1,
            questionTotal: 1,
            answerScore: 1,
            answerTotal: 1,
            percentage: 100,
            sendResultsAutomatically: false,
            resultsSent: false,
          },
        },
      }),
    );
    store.token.set(sessionToken);
    store.initQuestions([
      {
        id: 'question-1',
        phrase: { en: 'Question' },
        answers: [{ id: 'answer-1', phrase: { en: 'Answer' } }],
      },
    ]);
    store.selectAnswer('question-1', 'answer-1');

    facade.submit();
    firstResponse.error(new Error('connection lost after server commit'));

    expect(getRefTestByTokenFetch).toHaveBeenCalledWith({
      variables: { token: sessionToken },
      fetchPolicy: 'network-only',
    });
    expect(store.completed()).toBe(true);
    expect(store.result()).toMatchObject({
      questionScore: 1,
      questionTotal: 1,
      answerScore: 1,
      answerTotal: 1,
      percentage: 100,
    });
    expect(store.getSelectedAnswerIds()).toEqual(['answer-1']);
    expect(store.error()).toBeNull();

    facade.submit();
    expect(completeMutation).toHaveBeenCalledOnce();
  });

  it('keeps the submission snapshot available when reconciliation fails', () => {
    const firstResponse = new Subject<unknown>();
    completeMutation.mockReturnValue(firstResponse);
    getRefTestByTokenFetch.mockReturnValue(throwError(() => new Error('query failed')));
    store.token.set(sessionToken);
    store.currentLanguage.set('nl');
    store.initQuestions([
      {
        id: 'question-1',
        phrase: { en: 'Question' },
        answers: [{ id: 'answer-1', phrase: { en: 'Answer' } }],
      },
    ]);
    store.selectAnswer('question-1', 'answer-1');

    facade.submit();
    const originalInput = completeMutation.mock.calls[0][0].variables.input;
    firstResponse.error(new Error('submission response lost'));

    expect(store.error()).toBe('submit_failed');
    expect(store.completed()).toBe(false);
    expect(store.loading()).toBe(false);

    getRefTestByTokenFetch.mockReturnValue(
      of({
        data: {
          refTestByToken: {
            __typename: 'ParticipantRefTest',
            status: 'IN_PROGRESS',
          },
        },
      }),
    );
    completeMutation.mockReturnValue(
      of({ data: { completeRefTest: { participantRefTest: null } } }),
    );
    facade.submit();

    expect(completeMutation).toHaveBeenCalledTimes(2);
    expect(completeMutation.mock.calls[1][0].variables.input).toBe(originalInput);
  });

  it('flushes a pending progress save immediately and cancels its debounce', async () => {
    const saveResponse = new Subject<unknown>();
    saveProgressMutation.mockReturnValue(saveResponse);
    store.token.set(sessionToken);
    store.initQuestions([
      {
        id: 'question-1',
        phrase: { en: 'Question' },
        answers: [{ id: 'answer-1', phrase: { en: 'Answer' } }],
      },
    ]);
    store.selectAnswer('question-1', 'answer-1');

    vi.useFakeTimers();
    try {
      facade.triggerSave();
      expect(saveProgressMutation).not.toHaveBeenCalled();

      let saved: boolean | undefined;
      facade.flushPendingSave().subscribe((result) => (saved = result));
      expect(saveProgressMutation).toHaveBeenCalledOnce();
      expect(saveProgressMutation.mock.calls[0][0].variables.input).toMatchObject({
        token: sessionToken,
        currentQuestionIndex: 0,
        selectedAnswerIds: ['answer-1'],
      });

      saveResponse.next({
        data: {
          saveRefTestProgress: {
            participantRefTest: {
              id: 'ref-test',
              currentQuestionIndex: 0,
              selectedAnswerIds: ['answer-1'],
            },
          },
        },
      });
      saveResponse.complete();

      expect(saved).toBe(true);
      await vi.advanceTimersByTimeAsync(500);
      expect(saveProgressMutation).toHaveBeenCalledOnce();
    } finally {
      vi.useRealTimers();
    }
  });

  it('does not approve a pending progress save the API did not accept', () => {
    saveProgressMutation.mockReturnValue(
      of({ data: { saveRefTestProgress: { participantRefTest: null } } }),
    );
    store.token.set(sessionToken);

    let saved: boolean | undefined;
    facade.flushPendingSave().subscribe((result) => (saved = result));
    expect(saved).toBe(false);
  });
});
