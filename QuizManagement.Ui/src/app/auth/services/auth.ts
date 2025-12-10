import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { shareReplay } from 'rxjs';
import { User } from '../models/user';

@Injectable({
  providedIn: 'root',
})
export class Auth {
  private readonly _http = inject(HttpClient);

  readonly user = toSignal(this._http.get<User>('/Account/User').pipe(shareReplay(1)));

  login(): void {
    location.href = '/Account/Login';
  }

  logout(): void {
    location.href = '/Account/Logout';
  }
}
