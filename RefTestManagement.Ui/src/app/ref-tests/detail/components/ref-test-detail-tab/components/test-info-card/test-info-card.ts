import { ChangeDetectionStrategy, Component, input } from '@angular/core';
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
  title = input<RefTestTitle | undefined>(undefined);
  numberOfQuestions = input.required<number>();
  maxTimeInMinutes = input<number>();
  status = input.required<RefTestStatus>();
}
