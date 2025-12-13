import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { TranslationPipe } from '../../../../pipes/translation-pipe';
import { AnswerOptionComponent } from '../answer-option/answer-option';

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
  selector: 'app-question-card',
  templateUrl: './question-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, TranslationPipe, AnswerOptionComponent],
})
export class QuestionCardComponent {
  readonly question = input.required<Question>();
  readonly currentIndex = input.required<number>();
  readonly totalQuestions = input.required<number>();
  readonly currentLanguage = input.required<string>();
  readonly selectedAnswerIds = input.required<string[]>();
  readonly answerSelected = output<string>();

  protected isAnswerSelected(answerId: string): boolean {
    return this.selectedAnswerIds().includes(answerId);
  }
}
