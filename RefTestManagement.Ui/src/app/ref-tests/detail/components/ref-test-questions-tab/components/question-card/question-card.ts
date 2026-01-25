import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Question } from '../../../../../../../../graphql/generated';
import { TranslationPipe } from '../../../../../../pipes/translation-pipe';
import { AnswerItem } from '../answer-item/answer-item';

@Component({
  selector: 'app-question-card',
  imports: [TranslationPipe, AnswerItem],
  templateUrl: './question-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class QuestionCard {
  question = input.required<Question | null>();
  questionIndex = input.required<number>();
  currentLanguage = input.required<string>();
  selectedAnswerIds = input.required<Set<string>>();
}
