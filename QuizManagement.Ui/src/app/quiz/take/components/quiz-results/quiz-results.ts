import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { TranslationPipe } from '../../../../pipes/translation-pipe';

interface Answer {
  id: string;
  number?: string;
  phrase: Record<string, string>;
}

interface Question {
  id: string;
  number?: string;
  phrase: Record<string, string>;
  answers: Answer[];
}

@Component({
  selector: 'app-quiz-results',
  templateUrl: './quiz-results.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, TranslationPipe],
})
export class QuizResultsComponent {
  readonly questions = input.required<Question[]>();
  readonly score = input.required<number>();
  readonly percentage = input.required<number>();
  readonly wrongQuestionIds = input.required<string[]>();
  readonly wrongAnswerIds = input.required<string[]>();
  readonly selectedAnswers = input.required<Record<string, string[]>>();
  readonly currentLanguage = input.required<string>();
}
