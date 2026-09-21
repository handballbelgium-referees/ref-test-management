import { Component, computed, input } from '@angular/core';
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

  /**
   * Translation key for the spoken time warning, or an empty string for silence.
   *
   * Running out of time on an assessment is consequential, and the visible clock conveys it with
   * colour and position alone. Announcing it matters — but announcing every tick would make the
   * page unusable with a screen reader, which is why this resolves to one of only three values.
   *
   * The live region is driven by this key rather than the remaining seconds on purpose: the text
   * stays identical throughout each band, so the DOM only changes when a threshold is crossed and
   * the announcement happens exactly twice.
   */
  protected readonly timeWarningKey = computed(() => {
    const seconds = this.timeRemainingSeconds();

    if (seconds <= 0) return '';
    if (seconds <= 60) return 'ref_test.time_warning_one_minute';
    if (seconds <= 300) return 'ref_test.time_warning_five_minutes';

    return '';
  });
}
