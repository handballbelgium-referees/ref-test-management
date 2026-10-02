import { Location } from '@angular/common';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, Routes } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import {
  refTestInvitationFragmentGuard,
  refTestLegacyInvitationGuard,
  refTestSessionGuard,
  refTestTakeGuard,
} from './ref-test-token.guard';

@Component({ template: '<p>RefTest destination</p>' })
class RefTestDestination {}

@Component({ template: '<p>Home destination</p>' })
class HomeDestination {}

describe('RefTest token route guards', () => {
  const testRoutes: Routes = [
    {
      path: 'ref-test',
      pathMatch: 'full',
      component: RefTestDestination,
      canActivate: [refTestInvitationFragmentGuard],
    },
    {
      path: 'ref-test/welcome',
      pathMatch: 'full',
      component: RefTestDestination,
      canActivate: [refTestSessionGuard],
    },
    {
      path: 'ref-test/take',
      pathMatch: 'full',
      component: RefTestDestination,
      canActivate: [refTestTakeGuard],
    },
    {
      path: 'ref-test/:token',
      component: RefTestDestination,
      canActivate: [refTestLegacyInvitationGuard],
    },
    { path: '', component: HomeDestination },
    { path: '**', redirectTo: '' },
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter(testRoutes)] });
  });

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('redirects an unsupported legacy take URL to the home route', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/ref-test/participant-token/take', HomeDestination);

    expect(TestBed.inject(Router).url).toBe('/');
    expect(harness.routeNativeElement?.textContent).toContain('Home destination');
  });

  it('moves a fragment invitation into tokenless history state before loading welcome', async () => {
    const harness = await RouterTestingHarness.create();
    const invitationToken = 'a'.repeat(32);

    await harness.navigateByUrl(
      `/ref-test?lang=nl#${invitationToken}`,
      RefTestDestination,
    );

    expect(TestBed.inject(Router).url).toBe('/ref-test/welcome?lang=nl');
    expect(TestBed.inject(Location).getState()).toMatchObject({
      refTestToken: invitationToken,
    });
  });

  it.each(['/ref-test', '/ref-test#invalid'])(
    'rejects an invitation fragment that is missing or malformed: %s',
    async (url) => {
      const harness = await RouterTestingHarness.create();

      await harness.navigateByUrl(url, HomeDestination);

      expect(TestBed.inject(Router).url).toBe('/');
      expect(harness.routeNativeElement?.textContent).toContain('Home destination');
    },
  );

  it('migrates a legacy path invitation to the tokenless welcome route', async () => {
    const harness = await RouterTestingHarness.create();
    const invitationToken = 'b'.repeat(32);

    await harness.navigateByUrl(
      `/ref-test/${invitationToken}?lang=nl`,
      RefTestDestination,
    );

    expect(TestBed.inject(Router).url).toBe('/ref-test/welcome?lang=nl');
    expect(TestBed.inject(Location).getState()).toMatchObject({
      refTestToken: invitationToken,
    });
  });

  it('allows reloads when the current history entry holds a session credential', async () => {
    const harness = await RouterTestingHarness.create();
    TestBed.inject(Location).replaceState('/ref-test/take', '', {
      refTestSessionToken: 'rts1.history-token',
    });

    await harness.navigateByUrl('/ref-test/take', RefTestDestination);

    expect(TestBed.inject(Router).url).toBe('/ref-test/take');
    expect(harness.routeNativeElement?.textContent).toContain('RefTest destination');
  });

  it('routes legacy invitation state through the welcome exchange before taking the test', async () => {
    const harness = await RouterTestingHarness.create();
    TestBed.inject(Location).replaceState('/ref-test/take', '', {
      refTestToken: 'invitation-token',
    });

    await harness.navigateByUrl('/ref-test/take', RefTestDestination);

    expect(TestBed.inject(Router).url).toBe('/ref-test/welcome');
    expect(TestBed.inject(Location).getState()).toMatchObject({
      refTestToken: 'invitation-token',
    });
  });

  it('redirects tokenless direct visits to the home route', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/ref-test/take', HomeDestination);

    expect(TestBed.inject(Router).url).toBe('/');
    expect(harness.routeNativeElement?.textContent).toContain('Home destination');
  });
});
