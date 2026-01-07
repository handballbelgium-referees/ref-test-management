import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  HostListener,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { onlyCompleteData } from 'apollo-angular';
import { catchError, EMPTY, map, of, tap } from 'rxjs';
import {
  CompleteQuizSessionGQL,
  GetResultsEmailDelayMinutesGQL,
  GetScoreConfigurationGQL,
  StartQuizSessionGQL,
} from '../../../../graphql/generated';
import { QuizErrorComponent } from '../components/quiz-error/quiz-error';
import { LeaveQuizDialogComponent } from './components/leave-quiz-dialog/leave-quiz-dialog';
import { QuestionCardComponent } from './components/question-card/question-card';
import { QuizHeaderComponent } from './components/quiz-header/quiz-header';
import { QuizNavigationComponent } from './components/quiz-navigation/quiz-navigation';
import { QuizResultsComponent } from './components/quiz-results/quiz-results';
import { SubmitQuizDialog } from './components/submit-quiz-dialog/submit-quiz-dialog';

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
  selector: 'app-take-quiz',
  imports: [
    TranslatePipe,
    QuizHeaderComponent,
    QuestionCardComponent,
    QuizNavigationComponent,
    QuizResultsComponent,
    SubmitQuizDialog,
    LeaveQuizDialogComponent,
    QuizErrorComponent,
  ],
  templateUrl: './take-quiz.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class TakeQuizComponent {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _startSessionGQL = inject(StartQuizSessionGQL);
  private readonly _completeSessionGQL = inject(CompleteQuizSessionGQL);
  private readonly _translate = inject(TranslateService);

  private readonly _token = toSignal(
    this._route.paramMap.pipe(map((params) => params.get('token') ?? ''))
  );

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly questions = signal<IQuestion[]>([]);
  protected readonly currentQuestionIndex = signal(0);
  protected readonly visitedQuestions = signal<Set<number>>(new Set([0]));
  protected readonly selectedAnswers = signal<Record<string, string[]>>({});
  protected readonly startTime = signal<Date | null>(null);
  protected readonly maxTimeInMinutes = signal<number>(60);
  protected readonly timeRemainingSeconds = signal<number>(0);
  protected readonly quizCompleted = signal(false);
  protected readonly questionScore = signal<number | null>(null);
  protected readonly questionTotal = signal<number | null>(null);
  protected readonly answerScore = signal<number | null>(null);
  protected readonly answerTotal = signal<number | null>(null);
  protected readonly percentage = signal<number | null>(null);
  protected readonly showSubmitDialog = signal(false);
  protected readonly showLeaveDialog = signal(false);
  private _leaveConfirmed = false;
  private _tempLeaveHandlers?: { handleConfirm: () => void; handleCancel: () => void };

  protected readonly currentLanguage = toSignal(
    this._translate.onLangChange.pipe(map(() => this._translate.getCurrentLang())),
    {
      initialValue: this._translate.getCurrentLang(),
    }
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
        map((result) => result.data.scoreConfiguration.passingPercentage)
      ),
    { initialValue: 0 }
  );

  protected readonly emailDelayMinutes = toSignal(
    inject(GetResultsEmailDelayMinutesGQL)
      .watch()
      .valueChanges.pipe(
        onlyCompleteData(),
        map((result) => result.data.resultsEmailDelayMinutes)
      ),
    { initialValue: 0 }
  );

  @HostListener('window:beforeunload', ['$event'])
  beforeUnloadHandler(event: BeforeUnloadEvent): void {
    // Check if quiz is in progress (has questions loaded but not completed)
    if (this.questions().length > 0 && !this.quizCompleted()) {
      event.preventDefault();
    }
  }

  constructor() {
    // Start the quiz session when component initializes
    effect(() => {
      const token = this._token();
      if (token) {
        this.startQuiz(token);
      }
    });

    // Timer effect
    effect(() => {
      const startTime = this.startTime();
      if (!startTime || this.quizCompleted()) return;

      const interval = setInterval(() => {
        const now = new Date();
        const elapsedSeconds = Math.floor((now.getTime() - startTime.getTime()) / 1000);
        const totalSeconds = this.maxTimeInMinutes() * 60;
        const remaining = Math.max(0, totalSeconds - elapsedSeconds);
        this.timeRemainingSeconds.set(remaining);

        if (remaining === 0) {
          clearInterval(interval);
          this.confirmSubmitQuiz();
        }
      }, 1000);

      // Cleanup on destroy
      this._destroyRef.onDestroy(() => clearInterval(interval));
    });
  }

  private startQuiz(token: string): void {
    this.loading.set(true);
    this.error.set(null);

    this._startSessionGQL
      .mutate({
        variables: {
          input: { token },
        },
      })
      .pipe(
        tap((result) => this.loading.set(result.loading ?? false)),
        map((result) => result.data?.startQuizSession),
        tap((data) => {
          if (data?.errors && data.errors.length > 0) {
            const error = data.errors[0];
            if ('__typename' in error && error.__typename) {
              this.error.set(error.__typename);
            }
            return;
          }

          if (data?.quizSession && data.quizSession.questions) {
            const questions = data.quizSession.questions
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
            this.selectedAnswers.set(initialAnswers);

            // Set max time
            if (data.quizSession.maxTimeInMinutes) {
              this.maxTimeInMinutes.set(data.quizSession.maxTimeInMinutes);
            }

            // Calculate remaining time
            if (data.quizSession.startedAt) {
              const startTime = new Date(data.quizSession.startedAt);
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
        takeUntilDestroyed(this._destroyRef)
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
    }
  }

  protected nextQuestion(): void {
    if (this.canNext()) {
      const nextIndex = this.currentQuestionIndex() + 1;
      this.currentQuestionIndex.set(nextIndex);
      this.visitedQuestions.update((visited) => new Set([...visited, nextIndex]));
    }
  }

  protected requestSubmitQuiz(): void {
    this.showSubmitDialog.set(true);
  }

  protected cancelSubmit(): void {
    this.showSubmitDialog.set(false);
  }

  protected confirmSubmitQuiz(): void {
    this.showSubmitDialog.set(false);
    this.submitQuiz();
  }

  private submitQuiz(): void {
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

    this._completeSessionGQL
      .mutate({
        variables: {
          input: {
            token,
            selectedAnswerIds,
          },
        },
      })
      .pipe(
        tap((result) => this.loading.set(result.loading ?? false)),
        map((result) => result.data?.completeQuiz?.quizSession),
        tap((session) => {
          if (session) {
            this.quizCompleted.set(true);
            this.questionScore.set(session.questionScore ?? null);
            this.questionTotal.set(session.questionTotal ?? null);
            this.answerScore.set(session.answerScore ?? null);
            this.answerTotal.set(session.answerTotal ?? null);
            this.percentage.set(session.percentage ?? null);
          }
        }),
        catchError(() => {
          this.loading.set(false);
          this.error.set('submitFailed');

          return of(EMPTY);
        }),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe();
  }

  protected goToQuestion(index: number): void {
    if (this.visitedQuestions().has(index)) {
      this.currentQuestionIndex.set(index);
    }
  }

  protected isQuestionVisited(index: number): boolean {
    return this.visitedQuestions().has(index);
  }

  protected isQuestionAnswered(questionId: string): boolean {
    const answers = this.selectedAnswers()[questionId];
    return answers && answers.length > 0;
  }

  protected backToWelcome(): void {
    const token = this._token();
    if (token) {
      this._router.navigate(['/quiz', token]);
    }
  }

  canDeactivate(): boolean | Promise<boolean> {
    // Allow navigation if quiz is completed or user already confirmed leaving
    if (this.quizCompleted() || this._leaveConfirmed) {
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
