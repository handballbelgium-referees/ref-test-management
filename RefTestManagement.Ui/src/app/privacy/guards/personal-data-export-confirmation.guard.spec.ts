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
  template: '<p>Export confirmation destination</p>',
})
class ExportConfirmationDestination {}

describe('personalDataExportConfirmationGuard', () => {
  const confirmationComponentLoad = vi.fn();

  beforeEach(() => {
    confirmationComponentLoad.mockClear();

    const testRoutes = routes.map((route) => {
      if (route.path === 'privacy/export-confirmation') {
        return {
          ...route,
          loadComponent: () => {
            confirmationComponentLoad();
            return Promise.resolve(ExportConfirmationDestination);
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
  ])('redirects missing or empty confirmation fragments before component load: %s', async (url) => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl(url, PrivacyNoticeDestination);

    expect(TestBed.inject(Router).url).toBe('/privacy?lang=nl');
    expect(harness.routeNativeElement?.textContent).toContain('Privacy notice destination');
    expect(confirmationComponentLoad).not.toHaveBeenCalled();
  });

  it('allows a non-empty confirmation fragment to load the confirmation page', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl(
      '/privacy/export-confirmation?lang=nl#one-time-key',
      ExportConfirmationDestination,
    );

    expect(TestBed.inject(Router).url).toBe('/privacy/export-confirmation?lang=nl#one-time-key');
    expect(harness.routeNativeElement?.textContent).toContain('Export confirmation destination');
    expect(confirmationComponentLoad).toHaveBeenCalledOnce();
  });
});
