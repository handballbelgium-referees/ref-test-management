import { Location } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, CanDeactivate, Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Observable, Subscription, catchError, interval, map, of, take } from 'rxjs';
import {
  GetResultsEmailDelayMinutesGQL,
  GetScoreConfigurationGQL,
} from '../../../../graphql/generated';
import { RefTestError } from '../components/ref-test-error/ref-test-error';
import { WithdrawConsentDialog } from '../components/withdraw-consent-dialog/withdraw-consent-dialog';
import { LeaveRefTestDialog } from './components/leave-ref-test-dialog/leave-ref-test-dialog';
import { QuestionCard } from './components/question-card/question-card';
import { RefTestHeader } from './components/ref-test-header/ref-test-header';
import { RefTestNavigation } from './components/ref-test-navigation/ref-test-navigation';
import { RefTestResults } from './components/ref-test-results/ref-test-results';
import { RefTestWithdrawalQueued } from './components/ref-test-withdrawal-queued/ref-test-withdrawal-queued';
import { SubmitRefTestDialog } from './components/submit-ref-test-dialog/submit-ref-test-dialog';
import {
  REF_TEST_SESSION_TOKEN_STATE_KEY,
  resolveRefTestSessionToken,
} from '../ref-test-token-state';
import { RefTestFacade } from './state/ref-test.facade';
import { RefTestStore } from './state/ref-test.store';

@Component({
  selector: 'app-take-ref-test',
  templateUrl: './take-ref-test.html',
  providers: [RefTestStore, RefTestFacade],
  imports: [
    RefTestHeader,
    QuestionCard,
    RefTestNavigation,
    SubmitRefTestDialog,
    LeaveRefTestDialog,
    WithdrawConsentDialog,
    RefTestResults,
    RefTestError,
    RefTestWithdrawalQueued,
    TranslatePipe,
  ],
})
export class TakeRefTest implements CanDeactivate<TakeRefTest> {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _location = inject(Location);
  private readonly _scoreConfigGQL = inject(GetScoreConfigurationGQL);
  private readonly _emailDelayGQL = inject(GetResultsEmailDelayMinutesGQL);
  private readonly _translate = inject(TranslateService);

  readonly store = inject(RefTestStore);
  private readonly _facade = inject(RefTestFacade);

  private readonly _token = toSignal(
    this._route.paramMap.pipe(
      map(
        () =>
          resolveRefTestSessionToken(
            this._router.currentNavigation()?.extras.state,
            this._location.getState(),
          ) ?? '',
      ),
    ),
    { initialValue: '' },
  );
  private _leaveConfirmed = false;
  private _tempLeaveHandlers?: { confirm: () => void; cancel: () => void };
  readonly leaveSavePending = signal(false);
  readonly leaveSaveFailed = signal(false);

  readonly passingPercentage = toSignal(
    this._scoreConfigGQL
      .watch()
      .valueChanges.pipe(
        map((result) => {
          if (result.error) return null;

          const percentage = result.data?.scoreConfiguration?.passingPercentage;
          return typeof percentage === 'number' &&
            Number.isFinite(percentage) &&
            percentage >= 0 &&
            percentage <= 100
            ? percentage
            : null;
        }),
        catchError(() => of(null)),
      ),
    { initialValue: null },
  );

  readonly emailDelayMinutes = toSignal(
    this._emailDelayGQL.watch().valueChanges.pipe(map((r) => r.data?.resultsEmailDelayMinutes)),
    { initialValue: 0 },
  );

  readonly currentLanguage = this._translate.currentLang;

  constructor() {
    effect(() => {
      const lang = this._translate.getCurrentLang();
      this.store.currentLanguage.set(lang);
    });

    effect(() => {
      const token = this._token();
      if (token) this._facade.acquireSessionAndStart(token);
    });

    // Timer tick
    const tick = toSignal(interval(1000), { initialValue: -1 });

    effect(() => {
      const start = this.store.startTime();
      const completed = this.store.completed();
      const currentTick = tick();

      if (!start || completed || currentTick < 0) return;

      this.store.updateRemainingTime();

      // Auto-submit when timer reaches 0
      if (
        this.store.timeRemainingSeconds() === 0 &&
        !this.store.completed() &&
        this.store.beginAutoSubmit()
      ) {
        this._facade.submit();
      }
    });
  }

  isQuestionAnswered = (questionId: string) => this.store.selectedAnswers()[questionId]?.size > 0;
  isQuestionVisited = (index: number) => {
    const question = this.store.questions()[index];
    return question ? this.store.visitedQuestions().has(question.id) : false;
  };

  selectAnswer(qId: string, aId: string) {
    this.store.selectAnswer(qId, aId);
    this._facade.triggerSave();
  }

  next() {
    this.store.nextQuestion();
    this._facade.triggerSave();
  }

  previous() {
    this.store.previousQuestion();
    this._facade.triggerSave();
  }

  goTo(index: number) {
    this.store.goToQuestion(index);
    this._facade.triggerSave();
  }

  submit() {
    this._facade.submit();
  }

  back() {
    const token = this._token();
    if (token) {
      void this._router.navigate(['/ref-test/welcome'], {
        state: { [REF_TEST_SESSION_TOKEN_STATE_KEY]: token },
      });
    }
  }

  confirmLeave() {
    this._tempLeaveHandlers?.confirm();
  }

  cancelLeave() {
    this._tempLeaveHandlers?.cancel();
  }

  withdrawConsent() {
    this._facade.withdrawConsent();
  }

  canDeactivate(): boolean | Observable<boolean> {
    if (this.store.completed() || this._leaveConfirmed) return true;

    this.leaveSaveFailed.set(false);

    return new Observable<boolean>((subscriber) => {
      let settled = false;
      let saveSubscription: Subscription | undefined;
      const finish = (allowed: boolean) => {
        if (settled) return;

        settled = true;
        this.leaveSavePending.set(false);
        this.store.showLeaveDialog.set(false);
        this._tempLeaveHandlers = undefined;
        if (allowed) this._leaveConfirmed = true;
        subscriber.next(allowed);
        subscriber.complete();
      };

      this.store.showLeaveDialog.set(true);
      this._tempLeaveHandlers = {
        confirm: () => {
          if (settled || this.leaveSavePending()) return;

          this.leaveSavePending.set(true);
          saveSubscription = this._facade
            .flushPendingSave()
            .pipe(take(1))
            .subscribe({
              next: (saved) => {
                if (settled) return;
                if (saved) finish(true);
                else {
                  this.leaveSaveFailed.set(true);
                  finish(false);
                }
              },
              error: () => {
                if (settled) return;
                this.leaveSaveFailed.set(true);
                finish(false);
              },
            });
        },
        cancel: () => finish(false),
      };

      return () => {
        saveSubscription?.unsubscribe();
        if (settled) return;

        settled = true;
        this.leaveSavePending.set(false);
        this.store.showLeaveDialog.set(false);
        this._tempLeaveHandlers = undefined;
      };
    });
  }
}
