import { Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface IQuestion {
  id: string;
}

@Component({
  selector: 'app-ref-test-results',
  imports: [TranslatePipe],
  templateUrl: './ref-test-results.html',
  host: {
    class: 'block',
  },
})
export class RefTestResults {
  readonly questions = input.required<IQuestion[]>();
  readonly questionScore = input.required<number>();
  readonly questionTotal = input.required<number>();
  readonly answerScore = input.required<number>();
  readonly answerTotal = input.required<number>();
  readonly percentage = input.required<number>();
  readonly passingPercentage = input.required<number>();
  readonly emailDelayMinutes = input.required<number>();
  readonly sendResultsAutomatically = input.required<boolean>();
}
