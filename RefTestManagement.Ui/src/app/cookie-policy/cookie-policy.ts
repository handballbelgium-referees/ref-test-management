import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslatePipe } from '@ngx-translate/core';
import { IdentityProviderConfig } from './identity-provider-config';

@Component({
  selector: 'app-cookie-policy',
  imports: [TranslatePipe],
  templateUrl: './cookie-policy.html',
  host: {
    class: 'block',
  },
})
export class CookiePolicy {
  private readonly _identityProviderConfig = inject(IdentityProviderConfig);
  readonly providerName = toSignal(this._identityProviderConfig.getProviderName(), {
    initialValue: 'identity provider',
  });
}
