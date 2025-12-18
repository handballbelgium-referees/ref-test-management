import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { map, switchMap } from 'rxjs';
import { GetQuizSessionByTokenGQL } from '../../../../graphql/generated';
import { QuizErrorComponent } from '../components/quiz-error/quiz-error';
import { QuizHeroComponent } from './components/quiz-hero/quiz-hero';
import { QuizInstructionsComponent } from './components/quiz-instructions/quiz-instructions';
import { SessionDetailsComponent } from './components/session-details/session-details';

@Component({
  selector: 'app-quiz-welcome',
  imports: [
    TranslatePipe,
    QuizHeroComponent,
    QuizErrorComponent,
    SessionDetailsComponent,
    QuizInstructionsComponent,
  ],
  templateUrl: './quiz-welcome.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class QuizWelcomeComponent {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _getSessionGQL = inject(GetQuizSessionByTokenGQL);

  readonly sessionResult = toSignal(
    this._route.paramMap.pipe(
      map((params) => params.get('token') ?? ''),
      switchMap(
        (token) =>
          this._getSessionGQL.watch({
            variables: { token },
            fetchPolicy: 'cache-and-network',
          }).valueChanges
      )
    ),
    { initialValue: null }
  );

  readonly session = computed(() => {
    const result = this.sessionResult();
    if (!result?.data?.quizSessionByToken) return null;

    const data = result.data.quizSessionByToken;
    if (data.__typename === 'QuizSession') {
      return data;
    }
    return null;
  });

  readonly error = computed(() => {
    const result = this.sessionResult();
    if (!result?.data?.quizSessionByToken) return null;

    const data = result.data.quizSessionByToken;
    if (data.__typename !== 'QuizSession') {
      return data.__typename;
    }
    return null;
  });

  readonly loading = computed(() => this.sessionResult()?.loading ?? true);

  readonly canStart = computed(() => {
    const session = this.session();
    return session !== null && !this.loading();
  });

  private readonly _token = toSignal(
    this._route.paramMap.pipe(map((params) => params.get('token') ?? ''))
  );

  startQuiz(): void {
    const token = this._token();
    if (!token || !this.canStart()) return;

    this._router.navigate(['/quiz', token, 'take']);
  }
}
