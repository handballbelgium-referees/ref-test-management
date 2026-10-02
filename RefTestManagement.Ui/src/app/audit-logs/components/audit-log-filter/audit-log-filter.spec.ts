import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { AuditLogFilter } from './audit-log-filter';

describe('AuditLogFilter', () => {
  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [AuditLogFilter],
      providers: [provideTranslateService({ fallbackLang: 'en' })],
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      {
        'audit-logs': {
          eventType: {
            PersonalDataExportChallengeEmailDelivered: 'Verification email sent',
            PersonalDataExportChallengeEmailDeliveryFailed: 'Verification email failed',
            PersonalDataExportRequestVerified: 'Email address verified',
            PersonalDataExportDelivered: 'Export email sent',
            PersonalDataExportDeliveryFailed: 'Export delivery failed',
          },
          filter: {
            typeLabel: 'Event type',
          },
        },
      },
      true,
    );
    await new Promise<void>((resolve) => {
      translate.use('en').subscribe(() => resolve());
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  it('offers personal data export event types for filtering', () => {
    const fixture: ComponentFixture<AuditLogFilter> = TestBed.createComponent(AuditLogFilter);
    fixture.detectChanges();

    const select = fixture.nativeElement.querySelector(
      'select[title="Event type"]',
    ) as HTMLSelectElement;
    const options = Array.from(select.options);
    const values = options.map((option) => option.value);

    expect(values).toEqual(
      expect.arrayContaining([
        'PersonalDataExportChallengeEmailDelivered',
        'PersonalDataExportChallengeEmailDeliveryFailed',
        'PersonalDataExportRequestVerified',
        'PersonalDataExportDelivered',
        'PersonalDataExportDeliveryFailed',
      ]),
    );
    expect(options.map((option) => option.textContent?.trim())).toEqual(
      expect.arrayContaining([
        'Verification email sent',
        'Verification email failed',
        'Email address verified',
        'Export email sent',
        'Export delivery failed',
      ]),
    );
  });
});
