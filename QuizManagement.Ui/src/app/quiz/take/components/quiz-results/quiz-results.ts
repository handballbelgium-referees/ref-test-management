import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { TranslationPipe } from '../../../../pipes/translation-pipe';

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
  selector: 'app-quiz-results',
  templateUrl: './quiz-results.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, TranslationPipe],
})
export class QuizResultsComponent {
  readonly questions = input.required<IQuestion[]>();
  readonly score = input.required<number>();
  readonly percentage = input.required<number>();
  readonly wrongQuestionIds = input.required<string[]>();
  readonly wrongAnswerIds = input.required<string[]>();
  readonly selectedAnswers = input.required<Record<string, string[]>>();
  readonly currentLanguage = input.required<string>();
}
