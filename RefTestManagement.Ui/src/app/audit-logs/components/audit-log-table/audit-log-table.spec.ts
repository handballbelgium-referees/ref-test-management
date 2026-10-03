import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import type { AuditLogEntry } from '../../types';
import { AuditLogTable } from './audit-log-table';

describe('AuditLogTable', () => {
  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [AuditLogTable],
      providers: [provideRouter([]), provideTranslateService({ fallbackLang: 'nl' })],
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'nl',
      {
        'audit-logs': {
          actor: {
            system: 'Systeem',
            verifiedParticipant: 'Geverifieerde deelnemer',
          },
          entityType: {
            PersonalDataExportRequest: 'Gegevensexportaanvraag',
            PrivacyWithdrawalChallenge: 'Verificatie intrekking toestemming',
            PrivacyWithdrawalBatch: 'Verwerkingsbatch intrekking toestemming',
          },
          eventType: {
            PersonalDataExportRequestVerified: 'E-mailadres geverifieerd',
            PrivacyWithdrawalChallengeCreated: 'Intrekkingsverzoek vastgelegd',
            PrivacyWithdrawalChallengeEmailDelivered: 'Verificatiemail voor intrekking verzonden',
            PrivacyWithdrawalChallengeEmailDeliveryFailed:
              'Verificatiemail voor intrekking mislukt',
            PrivacyWithdrawalBatchConfirmed: 'Intrekkingsverwerking ingepland',
            PrivacyWithdrawalBatchCompleted: 'Verwerking intrekkingsbatch voltooid',
          },
        },
      },
      true,
    );
    await new Promise<void>((resolve) => {
      translate.use('nl').subscribe(() => resolve());
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  it('translates system and verified actors and preserves personal names in both layouts', async () => {
    const fixture: ComponentFixture<AuditLogTable> = TestBed.createComponent(AuditLogTable);
    const makeEntry = (seqId: number, actorName: string): AuditLogEntry => ({
      seqId,
      id: `id-${seqId}`,
      streamId: `stream-${seqId}`,
      version: 1,
      type: 'EntityModified',
      timestamp: '',
      actorName,
      actorEmail: '',
    });

    fixture.componentRef.setInput('entries', [
      makeEntry(1, 'System'),
      makeEntry(2, 'Verified participant'),
      makeEntry(3, 'Ada Lovelace'),
    ]);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text.match(/Systeem/g)?.length).toBe(2);
    expect(text.match(/Geverifieerde deelnemer/g)?.length).toBe(2);
    expect(text.match(/Ada Lovelace/g)?.length).toBe(2);
    expect(text).not.toContain('Verified participant');
    expect(text).not.toContain('System');
  });

  it('translates the export request entity type before its event type', async () => {
    const fixture: ComponentFixture<AuditLogTable> = TestBed.createComponent(AuditLogTable);
    fixture.componentRef.setInput('entries', [
      {
        seqId: 1,
        id: 'id-1',
        streamId: 'stream-1',
        version: 1,
        type: 'PersonalDataExportRequestVerified',
        timestamp: '',
        actorName: 'Verified participant',
        actorEmail: '',
        headers: JSON.stringify({ entityType: 'PersonalDataExportRequest' }),
      },
    ]);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const badge = fixture.nativeElement.querySelector('app-audit-log-event-badge') as HTMLElement;
    const text = badge.textContent ?? '';
    const entityTypeIndex = text.indexOf('Gegevensexportaanvraag');
    const eventTypeIndex = text.indexOf('E-mailadres geverifieerd');

    expect(entityTypeIndex).toBeGreaterThanOrEqual(0);
    expect(eventTypeIndex).toBeGreaterThan(entityTypeIndex);
  });

  it('translates withdrawal challenge and batch types on audit badges', async () => {
    const fixture: ComponentFixture<AuditLogTable> = TestBed.createComponent(AuditLogTable);
    fixture.componentRef.setInput('entries', [
      {
        seqId: 1,
        id: 'id-1',
        streamId: 'challenge-1',
        version: 1,
        type: 'PrivacyWithdrawalChallengeCreated',
        timestamp: '',
        actorName: 'Verified participant',
        actorEmail: '',
        headers: JSON.stringify({ entityType: 'PrivacyWithdrawalChallenge' }),
      },
      {
        seqId: 2,
        id: 'id-2',
        streamId: 'batch-1',
        version: 1,
        type: 'PrivacyWithdrawalBatchConfirmed',
        timestamp: '',
        actorName: 'Verified participant',
        actorEmail: '',
        headers: JSON.stringify({ entityType: 'PrivacyWithdrawalBatch' }),
      },
    ]);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const badges = Array.from(
      fixture.nativeElement.querySelectorAll('app-audit-log-event-badge'),
    ) as HTMLElement[];
    const badgeText = badges.map((badge) => badge.textContent ?? '');

    expect(badgeText[0]).toContain('Verificatie intrekking toestemming');
    expect(badgeText[0]).toContain('Intrekkingsverzoek vastgelegd');
    expect(badgeText[1]).toContain('Verwerkingsbatch intrekking toestemming');
    expect(badgeText[1]).toContain('Intrekkingsverwerking ingepland');
  });
});
