import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { signal } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { routes } from '../app.routes';
import { Auth } from '../auth/services/auth';
import { Permissions } from '../auth/models/permissions';
import { PermissionsService } from '../auth/services/permissions';

type AuthenticationState =
  | { status: 'checking' }
  | { status: 'authenticated' }
  | { status: 'unauthenticated' }
  | { status: 'error' };

@Component({ template: '<p>Home destination</p>' })
class HomeDestination {}

@Component({ template: '<p>Withdrawal review destination</p>' })
class WithdrawalReviewDestination {}

describe('privacy withdrawal review route', () => {
  let permissions: ReturnType<typeof signal<string[]>>;
  let authenticationState$: BehaviorSubject<AuthenticationState>;

  beforeEach(() => {
    permissions = signal<string[]>([]);
    authenticationState$ = new BehaviorSubject<AuthenticationState>({
      status: 'authenticated',
    });
    const testRoutes = routes.map((route) => {
      if (route.path === 'privacy/withdrawal-review') {
        return {
          ...route,
          loadComponent: () => Promise.resolve(WithdrawalReviewDestination),
        };
      }
      if (route.path === '') {
        return {
          ...route,
          loadComponent: () => Promise.resolve(HomeDestination),
        };
      }
      return route;
    });

    TestBed.configureTestingModule({
      providers: [
        provideRouter(testRoutes),
        {
          provide: Auth,
          useValue: { authenticationState$, login: vi.fn() },
        },
        {
          provide: PermissionsService,
          useValue: {
            permissions,
            hasPermission: (permission: string) => permissions().includes(permission),
          },
        },
      ],
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  it('redirects authenticated users without the review permission to the home route', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/privacy/withdrawal-review', HomeDestination);

    expect(TestBed.inject(Router).url).toBe('/');
    expect(harness.routeNativeElement?.textContent).toContain('Home destination');
  });

  it('allows an operator with the dedicated review permission to open the route', async () => {
    permissions.set([Permissions.PrivacyOperations.ReviewWithdrawals]);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/privacy/withdrawal-review', WithdrawalReviewDestination);

    expect(TestBed.inject(Router).url).toBe('/privacy/withdrawal-review');
    expect(harness.routeNativeElement?.textContent).toContain('Withdrawal review destination');
  });
});
