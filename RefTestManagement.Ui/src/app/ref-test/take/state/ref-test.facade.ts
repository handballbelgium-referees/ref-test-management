import { DestroyRef, Service, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  EMPTY,
  Observable,
  Subject,
  catchError,
  concatMap,
  debounce,
  defaultIfEmpty,
  distinctUntilChanged,
  filter,
  finalize,
  map,
  of,
  take,
  tap,
  timer,
} from 'rxjs';
import {
  CompleteRefTestGQL,
  CompleteRefTestMutation,
  GetRefTestByTokenGQL,
  GetRefTestByTokenQuery,
  RefTestSessionLockGQL,
  RefTestTimeExtendedGQL,
  SaveRefTestProgressGQL,
  StartRefTestGQL,
  StartRefTestMutation,
  WithdrawConsentGQL,
  WithdrawConsentMutation,
} from '../../../../../graphql/generated';
import { runMutation } from '../../../shared/utils/apollo-utils';
import { toSnakeCase } from '../../../shared/utils/string-utils';
import { Question as QuestionModel } from './ref-test.models';
import { RefTestStore } from './ref-test.store';

type StartRefTestPayload = StartRefTestMutation['startRefTest'];
type CompleteRefTestPayload = CompleteRefTestMutation['completeRefTest']['participantRefTest'];
type ParticipantRefTestByToken = Extract<
  GetRefTestByTokenQuery['refTestByToken'],
  { __typename: 'ParticipantRefTest' }
>;
type WithdrawConsentPayload = WithdrawConsentMutation['withdrawConsent'];

type SaveProgressRequest = {
  immediate: boolean;
  result?: Subject<boolean>;
};

@Service({ autoProvided: false })
export class RefTestFacade {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _store = inject(RefTestStore);

  private readonly _startRefTestGQL = inject(StartRefTestGQL);
  private readonly _completeRefTestGQL = inject(CompleteRefTestGQL);
  private readonly _getRefTestByTokenGQL = inject(GetRefTestByTokenGQL);
  private readonly _saveRefTestProgressGQL = inject(SaveRefTestProgressGQL);
  private readonly _refTestTimeExtendedGQL = inject(RefTestTimeExtendedGQL);
  private readonly _refTestSessionLockGQL = inject(RefTestSessionLockGQL);
  private readonly _withdrawConsentGQL = inject(WithdrawConsentGQL);

  private readonly _saveProgressTrigger = new Subject<SaveProgressRequest>();
  private _sessionStarted = false;
  private _submission?: { token: string; selectedAnswerIds: string[]; language: string };
  private _submissionInProgress = false;
  private _submissionReconciliationInProgress = false;

  constructor() {
    this._saveProgressTrigger
      .pipe(
        debounce((request) => (request.immediate ? of(0) : timer(500))),
        concatMap((request) => this.saveInternal().pipe(map((saved) => ({ request, saved })))),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe(({ request, saved }) => {
        request.result?.next(saved);
        request.result?.complete();
      });
  }

  acquireSessionAndStart(token: string): void {
    if (this._sessionStarted) return;
    this._sessionStarted = true;

    this._store.loading.set(true);

    this._getRefTestByTokenGQL
      .fetch({
        variables: { token },
        fetchPolicy: 'network-only',
      })
      .pipe(
        tap((result) => {
          const refTest = result.data?.refTestByToken;
          if (!refTest) {
            this._store.loading.set(false);
            this._store.error.set('general');
            return;
          }

          if (refTest.__typename !== 'ParticipantRefTest') {
            this._store.loading.set(false);
            this._store.error.set(toSnakeCase(refTest.__typename));
            return;
          }

          if (refTest.status === 'COMPLETED') {
            this.restoreCompleted(token, refTest);
            this._store.loading.set(false);
            return;
          }

          this.acquireSessionLockAndStart(token);
        }),
        catchError(() => {
          this._store.loading.set(false);
          this._store.error.set('general');
          return EMPTY;
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  private acquireSessionLockAndStart(token: string): void {
    const sessionId = crypto.randomUUID();

    this._refTestSessionLockGQL
      .subscribe({ variables: { token, sessionId } })
      .pipe(
        map((result) => result.data?.refTestSessionLock?.status),
        distinctUntilChanged(),
        tap((status) => {
          if (status === 'ACQUIRED') this.start(token);
          else if (status === 'BLOCKED') {
            this._store.loading.set(false);
            this._store.error.set('session_already_active');
          }
        }),
        catchError(() => {
          this._store.loading.set(false);
          this._store.error.set('general');
          return EMPTY;
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  start(token: string): void {
    this._store.error.set(null);

    runMutation<StartRefTestMutation, StartRefTestPayload | null>(
      this._startRefTestGQL.mutate({ variables: { input: { token } } }),
      this._destroyRef,
      {
        onStart: () => this._store.loading.set(true),
        onSuccess: (data) => {
          if (data) this.handleStart(token, data);
        },
        onError: () => this._store.error.set('general'),
        onComplete: () => this._store.loading.set(false),
      },
      (r) => r.data?.startRefTest ?? null,
    );
  }

  private handleStart(token: string, data?: StartRefTestPayload): void {
    const errors = data?.errors;
    if (errors && errors.length > 0) {
      this._store.error.set(toSnakeCase(errors[0].__typename!));
      return;
    }
    const refTest = data?.participantRefTest;
    if (!refTest?.questions) return;

    this._store.token.set(token);

    const questions = refTest.questions
      .filter((q) => !!q)
      .map(
        (q) =>
          ({
            id: q.id,
            phrase: q.phrase,
            answers: q.answers.map((a) => ({ id: a.id, phrase: a.phrase })),
          }) as QuestionModel,
      );

    this._store.initQuestions(questions);
    this._store.restoreProgress(
      refTest.selectedAnswerIds ?? [],
      refTest.currentQuestionIndex ?? 0,
      questions,
    );

    const maxTime = refTest.maxTimeInMinutes;
    const startTime = refTest.startedAt ? new Date(refTest.startedAt) : new Date();
    this._store.setTimer(startTime, maxTime);

    this.subscribeToTimeExtension(refTest.id);
  }

  submit(): void {
    if (
      this._submissionInProgress ||
      this._submissionReconciliationInProgress ||
      this._store.completed()
    ) {
      return;
    }

    const token = this._store.token();
    if (!token) return;

    const submission = (this._submission ??= {
      token,
      selectedAnswerIds: this._store.getSelectedAnswerIds(),
      language: this._store.currentLanguage() ?? 'en',
    });

    runMutation<CompleteRefTestMutation, CompleteRefTestPayload>(
      this._completeRefTestGQL.mutate({
        variables: { input: submission },
      }),
      this._destroyRef,
      {
        onStart: () => {
          this._submissionInProgress = true;
          this._store.error.set(null);
          this._store.loading.set(true);
        },
        onSuccess: (refTest) => {
          // A null payload means the server rejected the submission (an expired time limit, or
          // input it refused), not that nothing happened. Without this the participant sees the
          // spinner stop and no change at all.
          if (refTest) {
            this._store.complete(refTest);
            this._submission = undefined;
          } else this.reconcileSubmission(token);
        },
        onError: () => this.reconcileSubmission(token),
        onComplete: () => {
          this._submissionInProgress = false;
          if (!this._submissionReconciliationInProgress) this._store.loading.set(false);
        },
      },
      (r) => r.data?.completeRefTest?.participantRefTest ?? null,
    );
  }

  private restoreCompleted(token: string, refTest: ParticipantRefTestByToken): void {
    const questions =
      refTest.questions
        ?.filter((q) => !!q)
        .map(
          (q) =>
            ({
              id: q.id,
              number: q.number,
              phrase: q.phrase,
              answers: q.answers.map((a) => ({
                id: a.id,
                number: a.number,
                phrase: a.phrase,
                isCorrect: a.isCorrect,
              })),
            }) as QuestionModel,
        ) ?? [];

    this._store.restoreCompletedReview(questions, refTest.selectedAnswerIds ?? []);
    this._store.restoreCompletedResult(token, {
      questionScore: refTest.questionScore,
      questionTotal: refTest.questionTotal,
      answerScore: refTest.answerScore,
      answerTotal: refTest.answerTotal,
      percentage: refTest.percentage,
      sendResultsAutomatically: refTest.sendResultsAutomatically,
      resultsSent: refTest.resultsSent,
    });
    this._store.error.set(null);
    this._submission = undefined;
  }

  private reconcileSubmission(token: string): void {
    this._submissionReconciliationInProgress = true;
    this._store.error.set(null);
    this._store.loading.set(true);

    this._getRefTestByTokenGQL
      .fetch({
        variables: { token },
        fetchPolicy: 'network-only',
      })
      .pipe(
        map((result) => result.data?.refTestByToken),
        tap((refTest) => {
          if (
            refTest?.__typename === 'ParticipantRefTest' &&
            refTest.status === 'COMPLETED'
          ) {
            this.restoreCompleted(token, refTest);
          } else {
            this._store.error.set('submit_failed');
          }
        }),
        catchError(() => {
          this._store.error.set('submit_failed');
          return EMPTY;
        }),
        finalize(() => {
          this._submissionReconciliationInProgress = false;
          this._store.loading.set(false);
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  triggerSave(): void {
    if (!this._store.token()) return;
    this._saveProgressTrigger.next({ immediate: false });
  }

  private saveInternal(): Observable<boolean> {
    const token = this._store.token();
    const lang = this._store.currentLanguage();

    if (!token) return of(true);

    return this._saveRefTestProgressGQL
      .mutate({
        variables: {
          input: {
            token,
            currentQuestionIndex: this._store.currentQuestionIndex(),
            selectedAnswerIds: this._store.getSelectedAnswerIds(),
            language: lang ?? 'en',
          },
        },
      })
      .pipe(
        filter((result) => !result.loading),
        take(1),
        map(
          (result) =>
            !!result.data?.saveRefTestProgress?.participantRefTest && !result.error,
        ),
        defaultIfEmpty(false),
        catchError(() => of(false)),
      );
  }

  flushPendingSave(): Observable<boolean> {
    if (!this._store.token()) return of(true);

    return new Observable<boolean>((subscriber) => {
      const result = new Subject<boolean>();
      const resultSubscription = result.pipe(take(1)).subscribe(subscriber);
      this._saveProgressTrigger.next({ immediate: true, result });
      return resultSubscription;
    });
  }

  save(): void {
    this._saveProgressTrigger.next({ immediate: true });
  }

  private subscribeToTimeExtension(refTestId: string): void {
    this._refTestTimeExtendedGQL
      .subscribe({ variables: { id: refTestId } })
      .pipe(
        map((r) => r.data?.refTestTimeExtended?.newMaxTimeInMinutes),
        tap((minutes) => minutes && this._store.extendTime(minutes)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  withdrawConsent(): void {
    const token = this._store.token();
    if (!token) return;

    this._store.withdrawError.set(false);

    runMutation<WithdrawConsentMutation, WithdrawConsentPayload | null>(
      this._withdrawConsentGQL.mutate({ variables: { input: { token } } }),
      this._destroyRef,
      {
        onStart: () => this._store.withdrawing.set(true),
        onSuccess: (payload) => {
          const errors = payload?.errors;
          if (!payload?.boolean || (errors && errors.length > 0)) {
            this._store.withdrawError.set(true);
            return;
          }
          this._store.showWithdrawDialog.set(false);
          this._store.withdrawalQueued.set(true);
        },
        onError: () => this._store.withdrawError.set(true),
        onComplete: () => this._store.withdrawing.set(false),
      },
      (r) => r.data?.withdrawConsent ?? null,
    );
  }
}
