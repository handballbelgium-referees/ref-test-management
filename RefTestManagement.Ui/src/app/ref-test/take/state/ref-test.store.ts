import { computed, DestroyRef, inject, Service, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { Answer, Question, RefTestResult } from './ref-test.models';

/** How long the "your progress was restored" notice stays on screen. */
const PROGRESS_RESTORED_VISIBLE_MS = 10_000;

@Service({ autoProvided: false })
export class RefTestStore {
  private readonly _translate = inject(TranslateService);

  /**
   * Handle for the pending "progress restored" dismissal, so it can be cancelled.
   *
   * This store is provided by the take-RefTest component, so DestroyRef here is that component's:
   * the timer dies with the page that scheduled it.
   */
  private _progressRestoredTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this._progressRestoredTimer));
  }

  // === CORE STATE =====================================================
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly token = signal<string>('');
  readonly questions = signal<Question[]>([]);
  readonly currentQuestionIndex = signal(0);

  readonly selectedAnswers = signal<Record<string, Set<string>>>({});
  readonly visitedQuestions = signal<Set<string>>(new Set());

  readonly startTime = signal<Date | null>(null);
  readonly maxTimeInMinutes = signal(60);
  readonly timeRemainingSeconds = signal(0);

  readonly completed = signal(false);
  readonly result = signal<RefTestResult | null>(null);
  readonly sendResultsAutomatically = signal<boolean>(false);
  readonly autoSubmitAttempted = signal(false);

  readonly showSubmitDialog = signal(false);
  readonly showLeaveDialog = signal(false);
  readonly showProgressRestored = signal(false);

  readonly showWithdrawDialog = signal(false);
  readonly withdrawing = signal(false);
  readonly withdrawError = signal(false);
  readonly withdrawn = signal(false);

  // === CURRENT LANGUAGE ==============================================
  readonly currentLanguage = signal(this._translate.currentLang());

  // === COMPUTED =======================================================
  readonly currentQuestion = computed(() => {
    const qs = this.questions();
    return qs[this.currentQuestionIndex()] ?? null;
  });

  readonly progress = computed(() => {
    const total = this.questions().length;
    return total ? ((this.currentQuestionIndex() + 1) / total) * 100 : 0;
  });

  readonly answeredCount = computed(
    () => Object.values(this.selectedAnswers()).filter((s) => s.size > 0).length,
  );

  readonly canPrevious = computed(() => this.currentQuestionIndex() > 0);
  readonly canNext = computed(() => this.currentQuestionIndex() < this.questions().length - 1);
  readonly isLastQuestion = computed(
    () => this.currentQuestionIndex() === this.questions().length - 1,
  );

  readonly formattedTimeRemaining = computed(() => {
    const s = this.timeRemainingSeconds();
    const m = Math.floor(s / 60);
    const sec = s % 60;
    return `${m}:${sec.toString().padStart(2, '0')}`;
  });

  // === DOMAIN METHODS ==================================================
  initQuestions(questions: Question[]): void {
    this.questions.set(questions);
    const selected: Record<string, Set<string>> = {};
    questions.forEach((q) => (selected[q.id] = new Set()));
    this.selectedAnswers.set(selected);
  }

  restoreProgress(selectedAnswerIds: string[], index: number, questions: Question[]): void {
    const selected: Record<string, Set<string>> = {};
    questions.forEach((q) => (selected[q.id] = new Set()));

    selectedAnswerIds.forEach((answerId) => {
      const question = questions.find((q) => q.answers.some((a: Answer) => a.id === answerId));
      if (question) selected[question.id].add(answerId);
    });

    this.selectedAnswers.set(selected);
    this.currentQuestionIndex.set(index);

    const visited = new Set<string>(questions.slice(0, index + 1).map((q) => q.id));
    this.visitedQuestions.set(visited);

    if (index > 0 || selectedAnswerIds.length > 0) {
      this.showProgressRestored.set(true);
      this.scheduleProgressRestoredDismissal();
    }
  }

  /**
   * Hides the "progress restored" notice after a delay.
   *
   * The timer is cancelled on destroy and before rescheduling. Without that, navigating away
   * within the window left a callback holding this store alive to write a signal nobody was
   * reading, and restoring twice in quick succession left the first timer running to dismiss the
   * second notice early.
   */
  private scheduleProgressRestoredDismissal(): void {
    clearTimeout(this._progressRestoredTimer);
    this._progressRestoredTimer = setTimeout(
      () => this.showProgressRestored.set(false),
      PROGRESS_RESTORED_VISIBLE_MS,
    );
  }

  selectAnswer(questionId: string, answerId: string): void {
    this.selectedAnswers.update((state) => {
      const set = new Set(state[questionId] ?? []);
      set.has(answerId) ? set.delete(answerId) : set.add(answerId);
      return { ...state, [questionId]: set };
    });
  }

  goToQuestion(index: number): void {
    const question = this.questions()[index];
    if (!question || !this.visitedQuestions().has(question.id)) return;
    this.currentQuestionIndex.set(index);
  }

  nextQuestion(): void {
    if (!this.canNext()) return;
    const index = this.currentQuestionIndex() + 1;
    this.currentQuestionIndex.set(index);

    const question = this.questions()[index];
    if (question) {
      this.visitedQuestions.update((v) => new Set([...v, question.id]));
    }
  }

  previousQuestion(): void {
    if (this.canPrevious()) {
      this.currentQuestionIndex.update((i) => i - 1);
    }
  }

  setTimer(start: Date, maxMinutes: number): void {
    this.startTime.set(start);
    this.maxTimeInMinutes.set(maxMinutes);
    this.updateRemainingTime();
  }

  extendTime(newMinutes: number): void {
    this.maxTimeInMinutes.set(newMinutes);
    this.updateRemainingTime();
  }

  updateRemainingTime(): void {
    const start = this.startTime();
    if (!start) return;

    const elapsed = Math.floor((Date.now() - start.getTime()) / 1000);
    const total = this.maxTimeInMinutes() * 60;
    this.timeRemainingSeconds.set(Math.max(0, total - elapsed));
  }

  complete(result: RefTestResult): void {
    this.completed.set(true);
    this.result.set(result);
    this.sendResultsAutomatically.set(result.sendResultsAutomatically ?? false);
    this.showSubmitDialog.set(false);
  }

  beginAutoSubmit(): boolean {
    if (this.autoSubmitAttempted()) return false;

    this.autoSubmitAttempted.set(true);
    return true;
  }

  getSelectedAnswerIds(): string[] {
    return Object.values(this.selectedAnswers()).flatMap((s) => [...s]);
  }

  reset(): void {
    this.loading.set(false);
    this.error.set(null);
    this.questions.set([]);
    this.selectedAnswers.set({});
    this.visitedQuestions.set(new Set());
    this.currentQuestionIndex.set(0);
    this.completed.set(false);
    this.result.set(null);
    this.autoSubmitAttempted.set(false);
    this.showLeaveDialog.set(false);
    this.showSubmitDialog.set(false);
    this.showProgressRestored.set(false);
    this.showWithdrawDialog.set(false);
    this.withdrawing.set(false);
    this.withdrawError.set(false);
    this.withdrawn.set(false);
    this.startTime.set(null);
    this.maxTimeInMinutes.set(60);
    this.timeRemainingSeconds.set(0);
    this.token.set('');
  }
}
