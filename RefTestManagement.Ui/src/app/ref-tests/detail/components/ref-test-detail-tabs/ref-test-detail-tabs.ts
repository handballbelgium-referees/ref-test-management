import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { HasPermission } from '../../../../auth/directives/has-permission.directive';
import { Permissions } from '../../../../auth/models/permissions';

@Component({
  selector: 'app-ref-test-detail-tabs',
  imports: [RouterLink, RouterLinkActive, TranslatePipe, HasPermission],
  templateUrl: './ref-test-detail-tabs.html',
})
export class RefTestDetailTabs {
  protected readonly Permissions = Permissions;
}
