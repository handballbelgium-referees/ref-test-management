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
import { catchError, map, of, tap } from 'rxjs';
import { CompleteQuizSessionGQL, StartQuizSessionGQL } from '../../../../graphql/generated';
import { QuestionCardComponent } from './components/question-card/question-card';
import { QuizHeaderComponent } from './components/quiz-header/quiz-header';
import { QuizNavigationComponent } from './components/quiz-navigation/quiz-navigation';
import { QuizResultsComponent } from './components/quiz-results/quiz-results';

interface Answer {
  id: string;
  phrase: Record<string, string>;
}

interface Question {
  id: string;
  phrase: Record<string, string>;
  answers: Answer[];
}

@Component({
  selector: 'app-take-quiz',
  templateUrl: './take-quiz.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslatePipe,
    QuizHeaderComponent,
    QuestionCardComponent,
    QuizNavigationComponent,
    QuizResultsComponent,
  ],
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
  protected readonly questions = signal<Question[]>([]);
  protected readonly currentQuestionIndex = signal(0);
  protected readonly visitedQuestions = signal<Set<number>>(new Set([0]));
  protected readonly selectedAnswers = signal<Record<string, string[]>>({});
  protected readonly startTime = signal<Date | null>(null);
  protected readonly maxTimeInMinutes = signal<number>(60);
  protected readonly timeRemaining = signal<number>(0);
  protected readonly quizCompleted = signal(false);
  protected readonly score = signal<number | null>(null);
  protected readonly percentage = signal<number | null>(null);
  protected readonly wrongQuestionIds = signal<string[]>([]);
  protected readonly wrongAnswerIds = signal<string[]>([]);

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

  protected readonly canSubmit = computed(() => {
    const answeredCount = this.answeredCount();
    const totalQuestions = this.questions().length;
    return answeredCount === totalQuestions;
  });

  constructor() {
    // Start the quiz session when component initializes
    effect(
      () => {
        const token = this._token();
        if (token) {
          this.startQuiz(token);
        }
      },
      { allowSignalWrites: true }
    );

    // Timer effect
    effect(() => {
      const startTime = this.startTime();
      if (!startTime || this.quizCompleted()) return;

      const interval = setInterval(() => {
        const now = new Date();
        const elapsed = Math.floor((now.getTime() - startTime.getTime()) / 1000 / 60);
        const remaining = Math.max(0, this.maxTimeInMinutes() - elapsed);
        this.timeRemaining.set(remaining);

        if (remaining === 0) {
          clearInterval(interval);
          this.submitQuiz();
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
            if ('message' in error) {
              this.error.set(error.message);
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

            if (data.quizSession.startedAt) {
              this.startTime.set(new Date(data.quizSession.startedAt));
            }
            this.timeRemaining.set(this.maxTimeInMinutes());
          }
        }),
        catchError((err) => {
          this.loading.set(false);
          this.error.set(err.message || 'Failed to start quiz');
          return of(null);
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

  protected submitQuiz(): void {
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
            questionIds,
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
            this.score.set(session.score ?? null);
            this.percentage.set(session.percentage ?? null);
            this.wrongQuestionIds.set(session.wrongQuestionIds ?? []);
            this.wrongAnswerIds.set(session.wrongAnswerIds ?? []);
          }
        }),
        catchError((err) => {
          this.loading.set(false);
          this.error.set(err.message || 'Failed to submit quiz');
          return of(null);
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
}
