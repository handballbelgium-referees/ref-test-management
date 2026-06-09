import { Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-answer-item',
  imports: [TranslatePipe],
  templateUrl: './answer-item.html',
  host: { class: 'block' },
})
export class AnswerItem {
  readonly answerNumber = input<string>();
  readonly answerText = input.required<string>();
  readonly isCorrect = input.required<boolean>();
  readonly isSelected = input.required<boolean>();
}
