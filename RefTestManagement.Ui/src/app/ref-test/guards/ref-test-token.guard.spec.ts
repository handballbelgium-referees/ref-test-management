import { Location } from '@angular/common';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, Routes } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { refTestSessionGuard, refTestTokenUrlGuard } from './ref-test-token.guard';

@Component({ template: '<p>RefTest destination</p>' })
class RefTestDestination {}

@Component({ template: '<p>Home destination</p>' })
class HomeDestination {}

describe('RefTest token route guards', () => {
  const testRoutes: Routes = [
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
      canActivate: [refTestSessionGuard],
    },
    {
      path: 'ref-test/:token/take',
      component: RefTestDestination,
      canActivate: [refTestTokenUrlGuard],
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

  it('replaces a legacy token URL with a tokenless route and history state', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/ref-test/participant-token/take?lang=nl', RefTestDestination);

    expect(TestBed.inject(Router).url).toBe('/ref-test/take?lang=nl');
    expect(TestBed.inject(Location).getState()).toMatchObject({
      refTestToken: 'participant-token',
    });
    expect(harness.routeNativeElement?.textContent).toContain('RefTest destination');
  });

  it('allows reloads when the current history entry holds the token', async () => {
    const harness = await RouterTestingHarness.create();
    TestBed.inject(Location).replaceState('/ref-test/take', '', {
      refTestToken: 'history-token',
    });

    await harness.navigateByUrl('/ref-test/take', RefTestDestination);

    expect(TestBed.inject(Router).url).toBe('/ref-test/take');
    expect(harness.routeNativeElement?.textContent).toContain('RefTest destination');
  });

  it('redirects tokenless direct visits to the home route', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/ref-test/take', HomeDestination);

    expect(TestBed.inject(Router).url).toBe('/');
    expect(harness.routeNativeElement?.textContent).toContain('Home destination');
  });
});
