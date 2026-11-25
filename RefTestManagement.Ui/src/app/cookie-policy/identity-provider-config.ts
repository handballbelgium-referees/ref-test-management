import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';

interface IdentityProviderResponse {
  provider: string;
}

@Service()
export class IdentityProviderConfig {
  private readonly _http = inject(HttpClient);

  getProviderName(): Observable<string> {
    return this._http.get<IdentityProviderResponse>('/api/configuration/identity-provider').pipe(
      map(({ provider }) => provider),
      catchError(() => of('identity provider')),
    );
  }
}
