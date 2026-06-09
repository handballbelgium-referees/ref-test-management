import { Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-scores-card',
  imports: [TranslatePipe],
  templateUrl: './scores-card.html',
  host: { class: 'block' },
})
export class ScoresCard {
  readonly percentage = input.required<number>();
  readonly isPassed = input.required<boolean>();
  readonly questionScore = input.required<number>();
  readonly questionTotal = input.required<number>();
  readonly answerScore = input.required<number>();
  readonly answerTotal = input.required<number>();
}
