import { Component, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, CanDeactivate, Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { interval, map } from 'rxjs';
import {
  GetResultsEmailDelayMinutesGQL,
  GetScoreConfigurationGQL,
} from '../../../../graphql/generated';
import { RefTestError } from '../components/ref-test-error/ref-test-error';
import { LeaveRefTestDialog } from './components/leave-ref-test-dialog/leave-ref-test-dialog';
import { QuestionCard } from './components/question-card/question-card';
import { RefTestHeader } from './components/ref-test-header/ref-test-header';
import { RefTestNavigation } from './components/ref-test-navigation/ref-test-navigation';
import { RefTestResults } from './components/ref-test-results/ref-test-results';
import { SubmitRefTestDialog } from './components/submit-ref-test-dialog/submit-ref-test-dialog';
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
    RefTestResults,
    RefTestError,
    TranslatePipe,
  ],
})
export class TakeRefTest implements CanDeactivate<TakeRefTest> {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _scoreConfigGQL = inject(GetScoreConfigurationGQL);
  private readonly _emailDelayGQL = inject(GetResultsEmailDelayMinutesGQL);
  private readonly _translate = inject(TranslateService);

  readonly store = inject(RefTestStore);
  private readonly _facade = inject(RefTestFacade);

  private readonly _token = toSignal(this._route.paramMap.pipe(map((p) => p.get('token') ?? '')));
  private _leaveConfirmed = false;
  private _tempLeaveHandlers?: { confirm: () => void; cancel: () => void };

  readonly passingPercentage = toSignal(
    this._scoreConfigGQL
      .watch()
      .valueChanges.pipe(map((r) => r.data?.scoreConfiguration?.passingPercentage)),
    { initialValue: 0 },
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
      if (this.store.timeRemainingSeconds() === 0 && !this.store.completed()) {
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
    if (token) this._router.navigate(['/ref-test', token]);
  }

  confirmLeave() {
    this._tempLeaveHandlers?.confirm();
    this._tempLeaveHandlers = undefined;
  }

  cancelLeave() {
    this._tempLeaveHandlers?.cancel();
    this._tempLeaveHandlers = undefined;
  }

  canDeactivate(): boolean | Promise<boolean> {
    if (this.store.completed() || this._leaveConfirmed) return true;

    return new Promise((resolve) => {
      this.store.showLeaveDialog.set(true);
      this._tempLeaveHandlers = {
        confirm: () => {
          this._leaveConfirmed = true;
          this.store.showLeaveDialog.set(false);
          resolve(true);
        },
        cancel: () => {
          this.store.showLeaveDialog.set(false);
          resolve(false);
        },
      };
    });
  }
}
