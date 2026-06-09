import { Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-ref-test-header',
  imports: [TranslatePipe],
  templateUrl: './ref-test-header.html',
  host: {
    class: 'block',
  },
})
export class RefTestHeader {
  readonly timeRemaining = input.required<string>();
  readonly timeRemainingSeconds = input.required<number>();
  readonly answeredCount = input.required<number>();
  readonly totalQuestions = input.required<number>();
  readonly progress = input.required<number>();
}
