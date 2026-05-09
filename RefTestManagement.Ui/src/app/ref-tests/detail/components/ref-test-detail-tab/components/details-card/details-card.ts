import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestStatus } from '../../../../../../../../graphql/generated';
import { HasPermission } from '../../../../../../auth/directives/has-permission.directive';
import { Permissions } from '../../../../../../auth/models/permissions';

@Component({
  selector: 'app-details-card',
  imports: [TranslatePipe, HasPermission],
  templateUrl: './details-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class DetailsCard {
  readonly name = input.required<string>();
  readonly email = input.required<string>();
  readonly status = input.required<RefTestStatus>();
  protected readonly Permissions = Permissions;

  protected readonly edit = output<void>();
  protected readonly reset = output<void>();
  protected readonly revive = output<void>();
}
