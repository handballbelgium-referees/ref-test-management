import { registerLocaleData } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, RouterLink } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { GetPrivacyNoticeGQL } from '../../../graphql/generated';
import { PrivacyNotice } from './privacy-notice';

describe('PrivacyNotice', () => {
  const notice = {
    controllerName: 'Handball Belgium',
    controllerAddress: 'Arena 1, 1000 Brussels, Belgium',
    contactEmail: 'privacy@example.com',
    noticeVersion: 'v2.3',
    noticeEffectiveDate: '2026-09-22',
    retentionYears: 5,
  };
  const privacyNoticeWatch = vi.fn(() => ({
    valueChanges: of({
      data: {
        privacyNotice: notice,
      },
    }),
  }));

  beforeEach(async () => {
    const frenchLocale = await import('@angular/common/locales/fr');
    registerLocaleData(frenchLocale.default, 'fr-BE');

    privacyNoticeWatch.mockReset();
    privacyNoticeWatch.mockReturnValue({
      valueChanges: of({
        data: {
          privacyNotice: notice,
        },
      }),
    });
    TestBed.configureTestingModule({
      providers: [
        provideTranslateService({ fallbackLang: 'en' }),
        provideRouter([]),
        {
          provide: GetPrivacyNoticeGQL,
          useValue: {
            watch: privacyNoticeWatch,
          },
        },
      ],
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      {
        privacy: {
          title: 'Privacy notice',
          updated: 'Effective from {{effectiveDate}} (version {{noticeVersion}}).',
          controller: {
            title: 'Data controller and contact',
            body: '{{controllerName}}, {{controllerAddress}}. Contact {{contactEmail}}.',
          },
          data: { title: 'Data', body: 'Data body' },
          purposes: { title: 'Purposes', body: 'Purposes body' },
          basis: { title: 'Basis', body: 'Basis body' },
          recipients: { title: 'Recipients', body: 'Recipients body' },
          retention: { title: 'Retention', body: 'Retained for {{retentionYears}} years.' },
          rights: { title: 'Rights', body: 'Contact {{contactEmail}}.' },
          dataExport: {
            title: 'Request a copy of your personal data',
            description:
              'Enter the email address associated with your participation. If it matches a participant record, we will send a confirmation link to that address.',
            requestLink: 'Open the request form',
          },
        },
      },
      true,
    );
    await new Promise<void>((resolve) => {
      translate.use('en').subscribe(() => resolve());
    });
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    window.history.replaceState(window.history.state, '', window.location.pathname);
  });

  async function renderPrivacyNotice(): Promise<ComponentFixture<PrivacyNotice>> {
    const fixture = TestBed.createComponent(PrivacyNotice);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('renders the backend privacy notice metadata instead of hard-coded copy', async () => {
    const fixture = await renderPrivacyNotice();

    const text = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(text).toMatch(/Effective from .*2026.*\(version v2\.3\)\./);
    expect(text).toContain(
      'Handball Belgium, Arena 1, 1000 Brussels, Belgium. Contact privacy@example.com.',
    );
    expect(text).toContain('Retained for 5 years.');
    expect(text).toContain('Contact privacy@example.com.');
  });

  it('links to a separate export request page without rendering the request form or confirmation card', async () => {
    const fixture = await renderPrivacyNotice();
    const requestLink = fixture.debugElement.query(By.directive(RouterLink));
    if (!requestLink) throw new Error('The personal data export request link was not rendered.');

    const title = fixture.nativeElement.querySelector('#privacy-data-export-title');
    expect(title?.querySelector('a')).toBeNull();
    expect(title?.textContent).toContain('Request a copy of your personal data');
    expect(requestLink.nativeElement.getAttribute('href')).toContain('/privacy/export-request');
    expect(requestLink.injector.get(RouterLink).queryParamsHandling).toBe('preserve');
    expect(requestLink.nativeElement.textContent).toContain('Open the request form');
    expect(requestLink.nativeElement.closest('p')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    expect(fixture.nativeElement.querySelector('#privacy-data-export-email')).toBeNull();
    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-confirmation-title'),
    ).toBeNull();
  });
});
