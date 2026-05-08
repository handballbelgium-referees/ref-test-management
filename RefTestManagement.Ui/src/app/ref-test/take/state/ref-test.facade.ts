import { DestroyRef, Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, Subject, catchError, debounceTime, map, tap } from 'rxjs';
import {
  CompleteRefTestGQL,
  CompleteRefTestMutation,
  RefTestSessionLockGQL,
  RefTestTimeExtendedGQL,
  SaveRefTestProgressGQL,
  StartRefTestGQL,
  StartRefTestMutation,
} from '../../../../../graphql/generated';
import { runMutation } from '../../../shared/utils/apollo-utils';
import { toSnakeCase } from '../../../shared/utils/string-utils';
import { Question as QuestionModel } from './ref-test.models';
import { RefTestStore } from './ref-test.store';

type StartRefTestPayload = StartRefTestMutation['startRefTest'];
type CompleteRefTestPayload = CompleteRefTestMutation['completeRefTest']['refTest'];

@Injectable()
export class RefTestFacade {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _store = inject(RefTestStore);

  private readonly _startRefTestGQL = inject(StartRefTestGQL);
  private readonly _completeRefTestGQL = inject(CompleteRefTestGQL);
  private readonly _saveRefTestProgressGQL = inject(SaveRefTestProgressGQL);
  private readonly _refTestTimeExtendedGQL = inject(RefTestTimeExtendedGQL);
  private readonly _refTestSessionLockGQL = inject(RefTestSessionLockGQL);

  private readonly _saveProgress$ = new Subject<void>();

  constructor() {
    this._saveProgress$
      .pipe(debounceTime(500), takeUntilDestroyed(this._destroyRef))
      .subscribe(() => this.saveInternal());
  }

  acquireSessionAndStart(token: string): void {
    const sessionId = crypto.randomUUID();
    this._store.loading.set(true);

    this._refTestSessionLockGQL
      .subscribe({ variables: { token, sessionId } })
      .pipe(
        map((result) => result.data?.refTestSessionLock?.status),
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
    const refTest = data?.refTest;
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
    const token = this._store.token();
    if (!token) return;

    const selectedAnswerIds = this._store.getSelectedAnswerIds();

    runMutation<CompleteRefTestMutation, CompleteRefTestPayload>(
      this._completeRefTestGQL.mutate({
        variables: {
          input: { token, selectedAnswerIds, language: this._store.currentLanguage() },
        },
      }),
      this._destroyRef,
      {
        onStart: () => this._store.loading.set(true),
        onSuccess: (refTest) => {
          if (refTest) this._store.complete(refTest);
        },
        onError: () => this._store.error.set('submit_failed'),
        onComplete: () => this._store.loading.set(false),
      },
      (r) => r.data?.completeRefTest?.refTest ?? null,
    );
  }

  triggerSave(): void {
    if (!this._store.token()) return;
    this._saveProgress$.next();
  }

  private saveInternal() {
    const token = this._store.token();
    const lang = this._store.currentLanguage();

    if (!token) return;

    runMutation(
      this._saveRefTestProgressGQL.mutate({
        variables: {
          input: {
            token: token,
            currentQuestionIndex: this._store.currentQuestionIndex(),
            selectedAnswerIds: this._store.getSelectedAnswerIds(),
            language: lang,
          },
        },
      }),
      this._destroyRef,
    );
  }

  save(): void {
    this.saveInternal();
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
}
