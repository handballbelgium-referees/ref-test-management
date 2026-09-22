import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
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

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideTranslateService({ fallbackLang: 'en' }),
        {
          provide: GetPrivacyNoticeGQL,
          useValue: {
            watch: () => ({
              valueChanges: of({
                data: {
                  privacyNotice: notice,
                },
              }),
            }),
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
        },
      },
      true,
    );
    await new Promise<void>((resolve) => {
      translate.use('en').subscribe(() => resolve());
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  it('renders the backend privacy notice metadata instead of hard-coded copy', async () => {
    const fixture = TestBed.createComponent(PrivacyNotice);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(text).toMatch(/Effective from .*2026.*\(version v2\.3\)\./);
    expect(text).toContain('Handball Belgium, Arena 1, 1000 Brussels, Belgium. Contact privacy@example.com.');
    expect(text).toContain('Retained for 5 years.');
    expect(text).toContain('Contact privacy@example.com.');
  });
});
