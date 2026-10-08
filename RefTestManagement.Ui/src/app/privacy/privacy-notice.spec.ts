import { registerLocaleData } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, RouterLink } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { Observable, of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { GetPrivacyNoticeGQL } from '../../../graphql/generated';
import en from '../../../public/i18n/en.json';
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
  interface IPrivacyNoticeQueryResult {
    data?: { privacyNotice?: typeof notice | null };
    loading: boolean;
    error?: unknown;
  }
  const privacyNoticeWatch = vi.fn(
    (): { valueChanges: Observable<IPrivacyNoticeQueryResult> } => ({
      valueChanges: of({
        data: { privacyNotice: notice },
        loading: false,
      }),
    }),
  );

  beforeEach(async () => {
    const frenchLocale = await import('@angular/common/locales/fr');
    registerLocaleData(frenchLocale.default, 'fr-BE');

    privacyNoticeWatch.mockReset();
    privacyNoticeWatch.mockReturnValue({
      valueChanges: of({
        data: { privacyNotice: notice },
        loading: false,
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
    translate.setTranslation('en', en, true);
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
    const retentionCopy = en.privacy.retention.body.replace('{{retentionYears}}', '5');

    const text = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(text).toMatch(/Effective from .*2026.*\(version v2\.3\)\./);
    expect(text).toContain(
      en.privacy.controller.body
        .replace('{{controllerName}}', notice.controllerName)
        .replace('{{controllerAddress}}', notice.controllerAddress)
        .replace('{{contactEmail}}', notice.contactEmail),
    );
    expect(retentionCopy).toContain(
      'after creation for pending-approval or rejected records.',
    );
    expect(text).toContain(retentionCopy);
    expect(text).toContain(
      en.privacy.rights.body.replace('{{contactEmail}}', notice.contactEmail),
    );
  });

  it('explains that withdrawal redacts identifiers without permanently deleting the RefTest', async () => {
    const fixture = await renderPrivacyNotice();
    const text = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();
    const withdrawalCopy = en.privacy.consentWithdrawal.description;

    expect(text).toContain(withdrawalCopy);
    expect(withdrawalCopy).toContain('background processing redacts identifying details');
    expect(withdrawalCopy).toContain('A redacted RefTest record and audit trail may remain');
    expect(withdrawalCopy).toContain('removing it entirely is a separate step');
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

  it('links to a public withdrawal request form and preserves the selected locale', async () => {
    const fixture = await renderPrivacyNotice();
    const withdrawalLink = fixture.debugElement.queryAll(By.directive(RouterLink)).find((link) =>
      link.nativeElement.getAttribute('href')?.includes('/privacy/withdrawal-request'),
    );

    if (!withdrawalLink) throw new Error('The consent withdrawal request link was not rendered.');

    expect(withdrawalLink.nativeElement.textContent).toContain('Open the withdrawal request form');
    expect(withdrawalLink.injector.get(RouterLink).queryParamsHandling).toBe('preserve');
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
  });

  it('shows a successful missing-notice state instead of the loading state', async () => {
    privacyNoticeWatch.mockReturnValueOnce({
      valueChanges: of({ data: { privacyNotice: null }, loading: false }),
    });
    const emptyFixture = await renderPrivacyNotice();
    expect(emptyFixture.nativeElement.textContent).toContain(
      'The privacy notice is not available.',
    );
    expect(emptyFixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
    expect(emptyFixture.nativeElement.textContent).not.toContain('Loading privacy notice...');
  });

  it('shows query failures separately and retries them', async () => {
    privacyNoticeWatch
      .mockReturnValueOnce({
        valueChanges: of({ loading: false, error: new Error('query failed') }),
      })
      .mockReturnValueOnce({
        valueChanges: of({ data: { privacyNotice: notice }, loading: false }),
      });
    const errorFixture = await renderPrivacyNotice();
    expect(errorFixture.nativeElement.textContent).toContain('Could not load the privacy notice.');
    expect(errorFixture.nativeElement.textContent).not.toContain(
      'The privacy notice is not available.',
    );

    expect(privacyNoticeWatch).toHaveBeenCalledOnce();
    (errorFixture.nativeElement.querySelector('[role="alert"] button') as HTMLButtonElement).click();
    errorFixture.detectChanges();
    await errorFixture.whenStable();
    errorFixture.detectChanges();

    expect(privacyNoticeWatch).toHaveBeenCalledTimes(2);
    expect(errorFixture.nativeElement.textContent).toContain(
      en.privacy.controller.body
        .replace('{{controllerName}}', notice.controllerName)
        .replace('{{controllerAddress}}', notice.controllerAddress)
        .replace('{{contactEmail}}', notice.contactEmail),
    );
  });
});
