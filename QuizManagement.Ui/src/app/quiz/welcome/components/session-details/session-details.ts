import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DeepPartial } from '@apollo/client/utilities';
import { TranslatePipe } from '@ngx-translate/core';
import { GetQuizSessionByTokenQuery } from '../../../../../../graphql/generated';

type QuizSession = DeepPartial<
  Extract<GetQuizSessionByTokenQuery['quizSessionByToken'], { __typename?: 'QuizSession' }>
>;

@Component({
  selector: 'app-session-details',
  templateUrl: './session-details.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
})
export class SessionDetailsComponent {
  readonly session = input.required<QuizSession>();
}
