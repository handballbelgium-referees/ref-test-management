import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestStatus, RefTestTitle } from '../../../../../../../../graphql/generated';

@Component({
  selector: 'app-test-info-card',
  imports: [TranslatePipe],
  templateUrl: './test-info-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class TestInfoCard {
  readonly title = input<RefTestTitle>();
  readonly numberOfQuestions = input.required<number>();
  readonly maxTimeInMinutes = input<number>();
  readonly status = input.required<RefTestStatus>();
  protected readonly edit = output<void>();
  protected readonly regenerateToken = output<void>();

  protected readonly RefTestStatus = RefTestStatus;
}
