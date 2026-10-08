import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { BehaviorSubject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { GetAuditLogsGQL } from '../../../graphql/generated';
import { PermissionsService } from '../auth/services/permissions';
import { AuditLogData } from './audit-log-data';
import { ListAuditLogs } from './list-audit-logs';
import { AuditLogEntry } from './types';

interface IAuditLogsResult {
  data?: {
    auditLogs?: {
      edges: Array<{ node: AuditLogEntry }>;
      pageInfo: { hasNextPage: boolean; endCursor: string | null };
      totalCount: number;
    } | null;
  };
  loading: boolean;
  error?: unknown;
}

describe('ListAuditLogs', () => {
  let results: BehaviorSubject<IAuditLogsResult>;
  let queryRef: {
    valueChanges: BehaviorSubject<IAuditLogsResult>;
    getCurrentResult: () => IAuditLogsResult;
    fetchMore: ReturnType<typeof vi.fn>;
    refetch: ReturnType<typeof vi.fn>;
    setVariables: ReturnType<typeof vi.fn>;
  };
  let fixture: ComponentFixture<ListAuditLogs>;

  beforeEach(() => {
    results = new BehaviorSubject<IAuditLogsResult>({ loading: true });
    queryRef = {
      valueChanges: results,
      getCurrentResult: () => results.value,
      fetchMore: vi.fn(() => Promise.resolve({ data: {} })),
      refetch: vi.fn(() => Promise.resolve({ data: {} })),
      setVariables: vi.fn(),
    };
    TestBed.configureTestingModule({
      imports: [ListAuditLogs],
      providers: [
        provideRouter([]),
        provideTranslateService({ fallbackLang: 'en' }),
        AuditLogData,
        { provide: GetAuditLogsGQL, useValue: { watch: vi.fn(() => queryRef) } },
        { provide: PermissionsService, useValue: { hasPermission: () => false } },
      ],
    });
    TestBed.inject(TranslateService).setTranslation(
      'en',
      {
        common: { retry: 'Retry' },
        'audit-logs': {
          title: 'Audit Log',
          subtitle: 'System changes',
          loading: 'Loading audit log...',
          empty: 'No audit log events found',
          filtered_empty: 'No audit log events match these filters.',
          clear_filters: 'Clear filters',
          query_error: 'Could not load the audit log.',
          load_more_error: 'Could not load more audit log events.',
          loadMore: 'Load more',
          showing: 'Showing {{displayed}} of {{total}} event',
          showing_plural: 'Showing {{displayed}} of {{total}} events',
          filter: {
            title: 'Filters',
            streamIdLabel: 'Id',
            streamIdPlaceholder: 'Id',
            typeLabel: 'Event type',
            actorLabel: 'Actor',
            actorPlaceholder: 'Actor',
            allTypes: 'All event types',
            apply: 'Apply',
            clear: 'Clear',
          },
        },
      },
      true,
    );
    fixture = TestBed.createComponent(ListAuditLogs);
  });

  afterEach(() => TestBed.resetTestingModule());

  it('keeps loading, query failure, and a successful empty result distinct and retries failures', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading audit log...');

    results.next({ loading: false, error: new Error('query failed') });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Could not load the audit log.');
    expect(fixture.nativeElement.textContent).not.toContain('No audit log events found');
    (fixture.nativeElement.querySelector('[role="alert"] button') as HTMLButtonElement).click();
    expect(queryRef.refetch).toHaveBeenCalledOnce();

    results.next({
      loading: false,
      data: {
        auditLogs: {
          edges: [],
          pageInfo: { hasNextPage: false, endCursor: null },
          totalCount: 0,
        },
      },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No audit log events found');
    expect(fixture.nativeElement.textContent).not.toContain(
      'No audit log events match these filters.',
    );
    expect(fixture.nativeElement.textContent).not.toContain('Clear filters');
    expect(fixture.nativeElement.textContent).not.toContain('Could not load the audit log.');
  });

  it('shows a filtered-empty state and clears the filters to refresh the list', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const streamIdInput = fixture.nativeElement.querySelector(
      '#audit-log-filter-stream-id',
    ) as HTMLInputElement;
    streamIdInput.value = 'missing-stream';
    streamIdInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const auditData = TestBed.inject(AuditLogData);
    expect(auditData.filter()).not.toBeNull();

    results.next({
      loading: false,
      data: {
        auditLogs: {
          edges: [],
          pageInfo: { hasNextPage: false, endCursor: null },
          totalCount: 0,
        },
      },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'No audit log events match these filters.',
    );
    expect(fixture.nativeElement.textContent).not.toContain('No audit log events found');

    const clearButton = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Clear filters'));
    if (!clearButton) throw new Error('The filtered-empty clear button was not rendered.');
    clearButton.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(auditData.filter()).toBeNull();
    expect(streamIdInput.value).toBe('');
    expect(queryRef.setVariables).toHaveBeenLastCalledWith(
      expect.objectContaining({ where: null }),
    );
    expect(fixture.nativeElement.textContent).toContain('No audit log events found');
    expect(fixture.nativeElement.textContent).not.toContain(
      'No audit log events match these filters.',
    );
  });

  it('offers a retry after a later audit-log page fails', async () => {
    const entry: AuditLogEntry = {
      seqId: 1,
      id: 'event-1',
      streamId: 'stream-1',
      version: 1,
      type: 'EntityModified',
      timestamp: '',
      actorName: 'System',
      actorEmail: '',
    };
    results.next({
      loading: false,
      data: {
        auditLogs: {
          edges: [{ node: entry }],
          pageInfo: { hasNextPage: true, endCursor: 'cursor-1' },
          totalCount: 2,
        },
      },
    });
    queryRef.fetchMore.mockRejectedValueOnce(new Error('page failed'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    );
    const loadMore = buttons.find((button) => button.textContent?.includes('Load more'));
    if (!loadMore) throw new Error('The audit-log pagination button was not rendered.');
    loadMore.click();
    const auditData = TestBed.inject(AuditLogData);
    await vi.waitFor(() => expect(auditData.loadMoreError()).not.toBeNull());
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Could not load more audit log events.');

    queryRef.fetchMore.mockResolvedValue({ data: {} });
    (fixture.nativeElement.querySelector('[role="alert"] button') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(queryRef.fetchMore).toHaveBeenCalledTimes(2));
    await vi.waitFor(() => expect(auditData.loadMoreError()).toBeNull());
  });
});
