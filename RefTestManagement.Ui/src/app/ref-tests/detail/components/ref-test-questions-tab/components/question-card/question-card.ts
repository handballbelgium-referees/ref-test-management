import { Component, input } from '@angular/core';
import { TranslationPipe } from '../../../../../../pipes/translation-pipe';
import { IQuestion } from '../../models/question.interface';
import { AnswerItem } from '../answer-item/answer-item';

@Component({
  selector: 'app-question-card',
  imports: [TranslationPipe, AnswerItem],
  templateUrl: './question-card.html',
  host: { class: 'block' },
})
export class QuestionCard {
  readonly question = input.required<IQuestion>();
  readonly questionIndex = input.required<number>();
  readonly currentLanguage = input.required<string>();
  readonly selectedAnswerIds = input.required<Set<string>>();
}
