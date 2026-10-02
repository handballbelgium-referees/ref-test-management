import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { AuditLogEntryData } from './audit-log-entry-data';

describe('AuditLogEntryData', () => {
  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideTranslateService({ fallbackLang: 'en' })],
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      {
        'audit-logs': {
          failureCode: {
            SizeLimitExceeded: 'Export too large for email',
          },
          field: {
            failureCode: 'Failure reason',
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

  it('translates personal data export failure codes in audit details', async () => {
    const fixture: ComponentFixture<AuditLogEntryData> = TestBed.createComponent(AuditLogEntryData);
    fixture.componentRef.setInput('data', JSON.stringify({ failureCode: 'SizeLimitExceeded' }));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Failure reason');
    expect(fixture.nativeElement.textContent).toContain(
      'Export too large for email',
    );
  });
});
