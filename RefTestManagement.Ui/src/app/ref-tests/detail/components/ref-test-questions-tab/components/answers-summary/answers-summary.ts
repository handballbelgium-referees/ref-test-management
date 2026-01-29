import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-answers-summary',
  imports: [TranslatePipe],
  templateUrl: './answers-summary.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class AnswersSummary {
  readonly answeredQuestionsCount = input.required<number>();
  readonly totalQuestions = input.required<number>();
}
