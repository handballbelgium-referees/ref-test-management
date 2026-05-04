import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestStatus } from '../../../../../../../../graphql/generated';

@Component({
  selector: 'app-details-card',
  imports: [TranslatePipe],
  templateUrl: './details-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class DetailsCard {
  readonly name = input.required<string>();
  readonly email = input.required<string>();
  readonly status = input.required<RefTestStatus>();

  protected readonly edit = output<void>();
  protected readonly reset = output<void>();
  protected readonly revive = output<void>();
}
