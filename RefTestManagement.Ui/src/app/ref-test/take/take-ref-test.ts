import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { onlyCompleteData } from 'apollo-angular';
import { catchError, EMPTY, map, of, tap } from 'rxjs';
import {
  CompleteRefTestGQL,
  GetResultsEmailDelayMinutesGQL,
  GetScoreConfigurationGQL,
  RefTestTimeExtendedGQL,
  SaveRefTestProgressGQL,
  StartRefTestGQL,
} from '../../../../graphql/generated';
import { RefTestError } from '../components/ref-test-error/ref-test-error';
import { LeaveRefTestDialog } from './components/leave-ref-test-dialog/leave-ref-test-dialog';
import { QuestionCard } from './components/question-card/question-card';
import { RefTestHeader } from './components/ref-test-header/ref-test-header';
import { RefTestNavigation } from './components/ref-test-navigation/ref-test-navigation';
import { RefTestResults } from './components/ref-test-results/ref-test-results';
import { SubmitRefTestDialog } from './components/submit-ref-test-dialog/submit-ref-test-dialog';

interface IAnswer {
  id: string;
  number?: string;
  phrase: Record<string, string>;
}

interface IQuestion {
  id: string;
  number?: string;
  phrase: Record<string, string>;
  answers: IAnswer[];
}

@Component({
  selector: 'app-take-ref-test',
  imports: [
    TranslatePipe,
    RefTestHeader,
    QuestionCard,
    RefTestNavigation,
    RefTestResults,
    SubmitRefTestDialog,
    LeaveRefTestDialog,
    RefTestError,
  ],
  templateUrl: './take-ref-test.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class TakeRefTest {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _startRefTestGQL = inject(StartRefTestGQL);
  private readonly _completeRefTestGQL = inject(CompleteRefTestGQL);
  private readonly _saveRefTestProgressGQL = inject(SaveRefTestProgressGQL);
  private readonly _refTestTimeExtendedGQL = inject(RefTestTimeExtendedGQL);
  private readonly _translate = inject(TranslateService);

  private readonly _token = toSignal(
    this._route.paramMap.pipe(map((params) => params.get('token') ?? '')),
  );

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showProgressRestored = signal(false);
  protected readonly refTestId = signal<string | null>(null);
  protected readonly questions = signal<IQuestion[]>([]);
  protected readonly currentQuestionIndex = signal(0);
  protected readonly visitedQuestions = signal<Set<number>>(new Set([0]));
  protected readonly selectedAnswers = signal<Record<string, string[]>>({});
  protected readonly startTime = signal<Date | null>(null);
  protected readonly maxTimeInMinutes = signal<number>(60);
  protected readonly timeRemainingSeconds = signal<number>(0);
  protected readonly refTestCompleted = signal(false);
  protected readonly questionScore = signal<number | null>(null);
  protected readonly questionTotal = signal<number | null>(null);
  protected readonly answerScore = signal<number | null>(null);
  protected readonly answerTotal = signal<number | null>(null);
  protected readonly percentage = signal<number | null>(null);
  protected readonly showSubmitDialog = signal(false);
  protected readonly showLeaveDialog = signal(false);
  private _leaveConfirmed = false;
  private _tempLeaveHandlers?: { handleConfirm: () => void; handleCancel: () => void };
  private _refTestStarted = false;

  protected readonly currentLanguage = toSignal(
    this._translate.onLangChange.pipe(map(() => this._translate.getCurrentLang())),
    {
      initialValue: this._translate.getCurrentLang(),
    },
  );

  protected readonly currentQuestion = computed(() => {
    const questions = this.questions();
    const index = this.currentQuestionIndex();
    return questions[index] ?? null;
  });

  protected readonly answeredCount = computed(() => {
    const answers = this.selectedAnswers();
    return Object.values(answers).filter((arr) => arr.length > 0).length;
  });

  protected readonly progress = computed(() => {
    const total = this.questions().length;
    const current = this.currentQuestionIndex() + 1;
    return total > 0 ? (current / total) * 100 : 0;
  });

  protected readonly formattedTimeRemaining = computed(() => {
    const totalSeconds = this.timeRemainingSeconds();
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  });

  protected readonly canPrevious = computed(() => this.currentQuestionIndex() > 0);

  protected readonly canNext = computed(() => {
    const questions = this.questions();
    const index = this.currentQuestionIndex();
    return index < questions.length - 1;
  });

  protected readonly isLastQuestion = computed(() => {
    const questions = this.questions();
    const index = this.currentQuestionIndex();
    return index === questions.length - 1;
  });

  protected readonly passingPercentage = toSignal(
    inject(GetScoreConfigurationGQL)
      .watch()
      .valueChanges.pipe(
        onlyCompleteData(),
        map((result) => result.data.scoreConfiguration.passingPercentage),
      ),
    { initialValue: 0 },
  );

  protected readonly emailDelayMinutes = toSignal(
    inject(GetResultsEmailDelayMinutesGQL)
      .watch()
      .valueChanges.pipe(
        onlyCompleteData(),
        map((result) => result.data.resultsEmailDelayMinutes),
      ),
    { initialValue: 0 },
  );

  constructor() {
    // Start the RefTest when component initializes
    effect(() => {
      const token = this._token();
      if (token && !this._refTestStarted) {
        this._refTestStarted = true;
        this.startRefTest(token);
      }
    });

    // Timer effect
    effect(() => {
      const startTime = this.startTime();
      if (!startTime || this.refTestCompleted()) return;

      const interval = setInterval(() => {
        const now = new Date();
        const elapsedSeconds = Math.floor((now.getTime() - startTime.getTime()) / 1000);
        const totalSeconds = this.maxTimeInMinutes() * 60;
        const remaining = Math.max(0, totalSeconds - elapsedSeconds);
        this.timeRemainingSeconds.set(remaining);

        if (remaining === 0) {
          clearInterval(interval);
          this.confirmSubmitRefTest();
        }
      }, 1000);

      // Cleanup on destroy
      this._destroyRef.onDestroy(() => clearInterval(interval));
    });
  }

  private startRefTest(token: string): void {
    this.loading.set(true);
    this.error.set(null);

    this._startRefTestGQL
      .mutate({
        variables: {
          input: { token },
        },
      })
      .pipe(
        tap((result) => this.loading.set(result.loading ?? false)),
        map((result) => result.data?.startRefTest),
        tap((data) => {
          if (data?.errors && data.errors.length > 0) {
            const error = data.errors[0];
            if ('__typename' in error && error.__typename) {
              const snakeCaseValue = error.__typename
                .replace(/([A-Z])/g, '_$1')
                .toLowerCase()
                .replace(/^_/, '');
              this.error.set(snakeCaseValue);
            }
            return;
          }

          if (data?.refTest && data.refTest.questions) {
            // Store the ref test ID and subscribe to time extension updates
            const refTestId = data.refTest.id;
            this.refTestId.set(refTestId);

            this._refTestTimeExtendedGQL
              .subscribe({ variables: { id: refTestId } })
              .pipe(
                map((result) => result.data?.refTestTimeExtended),
                tap((timeExtendedData) => {
                  if (timeExtendedData?.newMaxTimeInMinutes) {
                    this.maxTimeInMinutes.set(timeExtendedData.newMaxTimeInMinutes);

                    // Recalculate remaining time with new max time
                    const startTime = this.startTime();
                    if (startTime) {
                      const now = new Date();
                      const elapsedSeconds = Math.floor(
                        (now.getTime() - startTime.getTime()) / 1000,
                      );
                      const totalSeconds = timeExtendedData.newMaxTimeInMinutes * 60;
                      const remaining = Math.max(0, totalSeconds - elapsedSeconds);
                      this.timeRemainingSeconds.set(remaining);
                    }
                  }
                }),
                takeUntilDestroyed(this._destroyRef),
              )
              .subscribe();

            const questions = data.refTest.questions
              .filter((q) => q && q.phrase && q.answers)
              .map((q) => ({
                id: q!.id,
                phrase: q!.phrase!,
                answers: q!.answers
                  .filter((a) => a && a.phrase)
                  .map((a) => ({
                    id: a!.id,
                    phrase: a!.phrase!,
                  })),
              }));

            this.questions.set(questions);

            // Initialize selectedAnswers with empty arrays for all questions
            const initialAnswers: Record<string, string[]> = {};
            questions.forEach((q) => {
              initialAnswers[q.id] = [];
            });

            // Restore saved progress if exists
            const savedAnswerIds = data.refTest.selectedAnswerIds ?? [];
            if (savedAnswerIds.length > 0) {
              // Map saved answer IDs to questions
              savedAnswerIds.forEach((answerId) => {
                // Find which question this answer belongs to
                const question = questions.find((q) => q.answers.some((a) => a.id === answerId));
                if (question) {
                  if (!initialAnswers[question.id]) {
                    initialAnswers[question.id] = [];
                  }
                  initialAnswers[question.id].push(answerId);
                }
              });
            }

            this.selectedAnswers.set(initialAnswers);

            // Restore current question index if saved
            const savedIndex = data.refTest.currentQuestionIndex ?? 0;
            this.currentQuestionIndex.set(savedIndex);

            // Mark all questions up to the saved index as visited
            const visited = new Set<number>();
            for (let i = 0; i <= savedIndex; i++) {
              visited.add(i);
            }
            this.visitedQuestions.set(visited);

            // Show restored progress message if we restored from a saved state
            if (savedIndex > 0 || savedAnswerIds.length > 0) {
              this.showProgressRestored.set(true);
              setTimeout(() => this.showProgressRestored.set(false), 10000);
            }

            // Set max time
            if (data.refTest.maxTimeInMinutes) {
              this.maxTimeInMinutes.set(data.refTest.maxTimeInMinutes);
            }

            // Calculate remaining time
            if (data.refTest.startedAt) {
              const startTime = new Date(data.refTest.startedAt);
              this.startTime.set(startTime);

              const now = new Date();
              const elapsedSeconds = Math.floor((now.getTime() - startTime.getTime()) / 1000);
              const totalSeconds = this.maxTimeInMinutes() * 60;
              const remaining = Math.max(0, totalSeconds - elapsedSeconds);
              this.timeRemainingSeconds.set(remaining);
            } else {
              this.timeRemainingSeconds.set(this.maxTimeInMinutes() * 60);
            }
          }
        }),
        catchError(() => {
          this.loading.set(false);
          this.error.set('general');

          return of(EMPTY);
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  protected selectAnswer(questionId: string, answerId: string): void {
    this.selectedAnswers.update((current) => {
      const currentAnswers = current[questionId] ?? [];
      const isSelected = currentAnswers.includes(answerId);

      return {
        ...current,
        [questionId]: isSelected
          ? currentAnswers.filter((id) => id !== answerId)
          : [...currentAnswers, answerId],
      };
    });
  }

  protected isAnswerSelected(questionId: string, answerId: string): boolean {
    const answers = this.selectedAnswers()[questionId] ?? [];
    return answers.includes(answerId);
  }

  protected previousQuestion(): void {
    if (this.canPrevious()) {
      this.currentQuestionIndex.update((i) => i - 1);
      this.saveProgress();
    }
  }

  protected nextQuestion(): void {
    if (this.canNext()) {
      const nextIndex = this.currentQuestionIndex() + 1;
      this.currentQuestionIndex.set(nextIndex);
      this.visitedQuestions.update((visited) => new Set([...visited, nextIndex]));
      this.saveProgress();
    }
  }

  protected requestSubmitRefTest(): void {
    this.showSubmitDialog.set(true);
  }

  protected cancelSubmit(): void {
    this.showSubmitDialog.set(false);
  }

  protected confirmSubmitRefTest(): void {
    this.showSubmitDialog.set(false);
    this.submitRefTest();
  }

  private submitRefTest(): void {
    const token = this._token();
    if (!token) return;

    const selectedAnswers = this.selectedAnswers();
    const questionIds: string[] = [];
    const selectedAnswerIds: string[] = [];

    // Flatten the answers - each answer needs a corresponding question ID
    Object.entries(selectedAnswers).forEach(([questionId, answerIds]) => {
      answerIds.forEach((answerId) => {
        questionIds.push(questionId);
        selectedAnswerIds.push(answerId);
      });
    });

    this.loading.set(true);

    this._completeRefTestGQL
      .mutate({
        variables: {
          input: {
            token,
            selectedAnswerIds,
            language: this.currentLanguage(),
          },
        },
      })
      .pipe(
        tap((result) => this.loading.set(result.loading ?? false)),
        map((result) => result.data?.completeRefTest?.refTest),
        tap((refTest) => {
          if (refTest) {
            this.refTestCompleted.set(true);
            this.questionScore.set(refTest.questionScore ?? null);
            this.questionTotal.set(refTest.questionTotal ?? null);
            this.answerScore.set(refTest.answerScore ?? null);
            this.answerTotal.set(refTest.answerTotal ?? null);
            this.percentage.set(refTest.percentage ?? null);
          }
        }),
        catchError(() => {
          this.loading.set(false);
          this.error.set('submit_failed');

          return of(EMPTY);
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  protected goToQuestion(index: number): void {
    if (this.visitedQuestions().has(index)) {
      this.currentQuestionIndex.set(index);
      this.saveProgress();
    }
  }

  private saveProgress(): void {
    const token = this._token();
    if (!token) return;

    const selectedAnswers = this.selectedAnswers();
    const selectedAnswerIds: string[] = [];

    // Flatten all selected answers
    Object.values(selectedAnswers).forEach((answerIds) => {
      selectedAnswerIds.push(...answerIds);
    });

    this._saveRefTestProgressGQL
      .mutate({
        variables: {
          input: {
            token,
            currentQuestionIndex: this.currentQuestionIndex(),
            selectedAnswerIds,
            language: this.currentLanguage(),
          },
        },
      })
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe();
  }

  protected isQuestionVisited(index: number): boolean {
    return this.visitedQuestions().has(index);
  }

  protected isQuestionAnswered(questionId: string): boolean {
    const answers = this.selectedAnswers()[questionId];
    return answers && answers.length > 0;
  }

  protected back_to_welcome(): void {
    const token = this._token();
    if (token) {
      this._router.navigate(['/ref-test', token]);
    }
  }

  canDeactivate(): boolean | Promise<boolean> {
    // Allow navigation if RefTest is completed or user already confirmed leaving
    if (this.refTestCompleted() || this._leaveConfirmed) {
      return true;
    }

    // Show dialog and return a promise
    return new Promise<boolean>((resolve) => {
      this.showLeaveDialog.set(true);

      // Create a temporary handler to resolve the promise
      const handleConfirm = () => {
        this._leaveConfirmed = true;
        this.showLeaveDialog.set(false);
        resolve(true);
      };

      const handleCancel = () => {
        this.showLeaveDialog.set(false);
        resolve(false);
      };

      // Store handlers temporarily
      this._tempLeaveHandlers = { handleConfirm, handleCancel };
    });
  }

  protected confirmLeave(): void {
    const handlers = this._tempLeaveHandlers;
    if (handlers) {
      handlers.handleConfirm();
      this._tempLeaveHandlers = undefined;
    } else {
      // If no handlers (e.g., dialog shown but not from canDeactivate), just close dialog
      this.showLeaveDialog.set(false);
    }
  }

  protected cancelLeave(): void {
    const handlers = this._tempLeaveHandlers;
    if (handlers) {
      handlers.handleCancel();
      this._tempLeaveHandlers = undefined;
    } else {
      // If no handlers (e.g., dialog shown but not from canDeactivate), just close dialog
      this.showLeaveDialog.set(false);
    }
  }
}
