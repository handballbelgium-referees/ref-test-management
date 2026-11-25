import { Service, inject } from '@angular/core';
import { GetIdentityProviderGQL } from '../../../graphql/generated';
import { Observable, catchError, map, of } from 'rxjs';

@Service()
export class IdentityProviderConfig {
  private readonly _getIdentityProvider = inject(GetIdentityProviderGQL);

  getProviderName(): Observable<string> {
    return this._getIdentityProvider.fetch().pipe(
      map(({ data }) => data?.identityProvider ?? 'identity provider'),
      catchError(() => of('identity provider')),
    );
  }
}
