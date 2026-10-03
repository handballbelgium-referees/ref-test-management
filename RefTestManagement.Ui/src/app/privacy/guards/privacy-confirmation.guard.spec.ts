import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { routes } from '../../app.routes';

@Component({
  template: '<p>Privacy notice destination</p>',
})
class PrivacyNoticeDestination {}

@Component({
  template: '<p>Confirmation destination</p>',
})
class ConfirmationDestination {}

describe('privacyConfirmationGuard', () => {
  const confirmationComponentLoad = vi.fn();

  beforeEach(() => {
    confirmationComponentLoad.mockClear();

    const testRoutes = routes.map((route) => {
      if (
        route.path === 'privacy/export-confirmation' ||
        route.path === 'privacy/withdrawal-confirmation'
      ) {
        return {
          ...route,
          loadComponent: () => {
            confirmationComponentLoad();
            return Promise.resolve(ConfirmationDestination);
          },
        };
      }

      if (route.path === 'privacy') {
        return {
          ...route,
          loadComponent: () => Promise.resolve(PrivacyNoticeDestination),
        };
      }

      return route;
    });

    TestBed.configureTestingModule({
      providers: [provideRouter(testRoutes)],
    });
  });

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it.each([
    '/privacy/export-confirmation?lang=nl',
    '/privacy/export-confirmation?lang=nl#',
    '/privacy/export-confirmation?lang=nl#%20%20',
    '/privacy/withdrawal-confirmation?lang=nl',
    '/privacy/withdrawal-confirmation?lang=nl#',
    '/privacy/withdrawal-confirmation?lang=nl#%20%20',
  ])('redirects missing or empty confirmation fragments before component load: %s', async (url) => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl(url, PrivacyNoticeDestination);

    expect(TestBed.inject(Router).url).toBe('/privacy?lang=nl');
    expect(harness.routeNativeElement?.textContent).toContain('Privacy notice destination');
    expect(confirmationComponentLoad).not.toHaveBeenCalled();
  });

  it.each([
    '/privacy/export-confirmation?lang=nl#one-time-key',
    '/privacy/withdrawal-confirmation?lang=nl#one-time-key',
  ])('allows a non-empty confirmation fragment to load the confirmation page: %s', async (url) => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl(url, ConfirmationDestination);

    expect(TestBed.inject(Router).url).toBe(url);
    expect(harness.routeNativeElement?.textContent).toContain('Confirmation destination');
    expect(confirmationComponentLoad).toHaveBeenCalledOnce();
  });
});
