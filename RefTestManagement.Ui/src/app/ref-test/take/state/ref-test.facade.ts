import { DestroyRef, Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, Subject, catchError, debounceTime, filter, finalize, map, tap } from 'rxjs';
import {
  Answer,
  CompleteRefTestGQL,
  Question,
  RefTestTimeExtendedGQL,
  SaveRefTestProgressGQL,
  StartRefTestGQL,
  StartRefTestPayload,
} from '../../../../../graphql/generated';
import { toSnakeCase } from '../../../shared/utils/string-utils';
import { Question as QuestionModel } from './ref-test.models';
import { RefTestStore } from './ref-test.store';

@Injectable()
export class RefTestFacade {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _store = inject(RefTestStore);

  private readonly _startRefTestGQL = inject(StartRefTestGQL);
  private readonly _completeRefTestGQL = inject(CompleteRefTestGQL);
  private readonly _saveRefTestProgressGQL = inject(SaveRefTestProgressGQL);
  private readonly _refTestTimeExtendedGQL = inject(RefTestTimeExtendedGQL);

  private readonly _saveProgress$ = new Subject<void>();

  constructor() {
    this._saveProgress$
      .pipe(debounceTime(500), takeUntilDestroyed(this._destroyRef))
      .subscribe(() => this.saveInternal());
  }

  start(token: string): void {
    this._store.loading.set(true);
    this._store.error.set(null);

    this._startRefTestGQL
      .mutate({ variables: { input: { token } } })
      .pipe(
        map((r) => r.data?.startRefTest),
        filter((data): data is StartRefTestPayload => !!data),
        tap((data) => this.handleStart(token, data)),
        catchError(() => {
          this._store.error.set('general');
          return EMPTY;
        }),
        tap(() => this._store.loading.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
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
        (q: Question) =>
          ({
            id: q.id,
            phrase: q.phrase,
            answers: q.answers.map((a: Answer) => ({ id: a.id, phrase: a.phrase })),
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

    this._completeRefTestGQL
      .mutate({
        variables: {
          input: { token, selectedAnswerIds, language: this._store.currentLanguage() },
        },
      })
      .pipe(
        tap((r) => this._store.loading.set(r.loading ?? false)),
        map((r) => r.data?.completeRefTest?.refTest),
        tap((refTest) => this._store.complete(refTest ?? {})),
        catchError(() => {
          this._store.error.set('submit_failed');
          return EMPTY;
        }),
        finalize(() => this._store.loading.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  triggerSave(): void {
    if (!this._store.token()) return;
    this._saveProgress$.next();
  }

  private saveInternal() {
    const token = this._store.token();
    const lang = this._store.currentLanguage();

    if (!token) return;

    this._saveRefTestProgressGQL
      .mutate({
        variables: {
          input: {
            token: token,
            currentQuestionIndex: this._store.currentQuestionIndex(),
            selectedAnswerIds: this._store.getSelectedAnswerIds(),
            language: lang,
          },
        },
      })
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe();
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
