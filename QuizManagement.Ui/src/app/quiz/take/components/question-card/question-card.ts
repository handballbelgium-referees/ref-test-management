import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { TranslationPipe } from '../../../../pipes/translation-pipe';
import { AnswerOptionComponent } from '../answer-option/answer-option';

interface IAnswer {
  id: string;
  phrase: Record<string, string>;
}

interface IQuestion {
  id: string;
  phrase: Record<string, string>;
  answers: IAnswer[];
}

@Component({
  selector: 'app-question-card',
  imports: [TranslatePipe, TranslationPipe, AnswerOptionComponent],
  templateUrl: './question-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class QuestionCardComponent {
  readonly question = input.required<IQuestion>();
  readonly currentIndex = input.required<number>();
  readonly totalQuestions = input.required<number>();
  readonly currentLanguage = input.required<string>();
  readonly selectedAnswerIds = input.required<string[]>();
  readonly answerSelected = output<string>();

  protected isAnswerSelected(answerId: string): boolean {
    return this.selectedAnswerIds().includes(answerId);
  }
}
