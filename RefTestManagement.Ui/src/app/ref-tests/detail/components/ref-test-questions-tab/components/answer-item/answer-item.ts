import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-answer-item',
  imports: [TranslatePipe],
  templateUrl: './answer-item.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class AnswerItem {
  answerNumber = input<string | null | undefined>(null);
  answerText = input.required<string>();
  isCorrect = input.required<boolean>();
  isSelected = input.required<boolean>();
}
