import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { firstValueFrom, Observable, Subject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { authGuard } from './auth-guard';
import { Auth } from '../services/auth';

type AuthenticationState =
  | { status: 'checking' }
  | { status: 'authenticated' }
  | { status: 'unauthenticated' }
  | { status: 'error' };

describe('authGuard', () => {
  let authenticationState$: Subject<AuthenticationState>;
  let login: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    authenticationState$ = new Subject<AuthenticationState>();
    login = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: Auth, useValue: { authenticationState$, login } },
      ],
    });
  });

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  function runGuard(): Observable<boolean | UrlTree> {
    return TestBed.runInInjectionContext(
      () =>
        authGuard(
          {} as ActivatedRouteSnapshot,
          { url: '/protected' } as RouterStateSnapshot,
        ) as Observable<boolean | UrlTree>,
    );
  }

  it('allows access after authentication succeeds', async () => {
    const result = firstValueFrom(runGuard());
    authenticationState$.next({ status: 'checking' });
    authenticationState$.next({ status: 'authenticated' });

    await expect(result).resolves.toBe(true);
    expect(login).not.toHaveBeenCalled();
  });

  it('starts login only after the check confirms the user is unauthenticated', async () => {
    const result = firstValueFrom(runGuard());
    authenticationState$.next({ status: 'unauthenticated' });

    await expect(result).resolves.toBe(false);
    expect(login).toHaveBeenCalledOnce();
  });

  it('fails closed and returns to the recovery page after a check failure', async () => {
    const result = firstValueFrom(runGuard());
    authenticationState$.next({ status: 'checking' });
    authenticationState$.next({ status: 'error' });

    const guardResult = await result;
    expect(guardResult).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(guardResult as UrlTree)).toBe('/');
    expect(login).not.toHaveBeenCalled();
  });
});
