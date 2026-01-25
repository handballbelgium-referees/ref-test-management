import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-scores-card',
  imports: [TranslatePipe],
  templateUrl: './scores-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class ScoresCard {
  percentage = input.required<number>();
  isPassed = input.required<boolean>();
  questionScore = input.required<number>();
  questionTotal = input.required<number>();
  answerScore = input.required<number>();
  answerTotal = input.required<number>();
}
